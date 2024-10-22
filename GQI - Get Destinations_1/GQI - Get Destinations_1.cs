/*
****************************************************************************
*  Copyright (c) 2024,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

dd/mm/2024	1.0.0.1		XXX, Skyline	Initial version
****************************************************************************
*/

namespace GQI_GetDestinations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Reflection.Emit;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Helper;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.Messages.SLDataGateway;

    [GQIMetaData(Name = "GQI - Get Destinations")]
    public sealed class GetSource : IGQIDataSource, IGQIOnInit, IGQIInputArguments
    {
        private readonly GQIStringArgument selectedElementArg = new GQIStringArgument("Selected Source Element Name") { IsRequired = false, DefaultValue = string.Empty };
        private readonly GQIStringArgument routingModeArg = new GQIStringArgument("Routing Mode") { IsRequired = true };
        private readonly GQIStringArgument siteLocationeArg = new GQIStringArgument("Site Location") { IsRequired = false, DefaultValue = string.Empty };
        private readonly GQIStringArgument srtModeArg = new GQIStringArgument("SRT Mode") { IsRequired = false, DefaultValue = string.Empty };

        private readonly Dictionary<string, string> exceptionsDict = new Dictionary<string, string>
        {
            {"-1","N/A"},
            {"-2", "N/A" },
        };

        private readonly Dictionary<int, string> StateDict = new Dictionary<int, string>
        {
            {1, "Enabled"},
            {0, "Disabled" },
        };

        private string _routingMode;
        private string _siteLocation;
        private string _srtMode;
        private string _selectedElement;
        private GQIDMS _dms;
        private List<ElementHelper> elementHelperList = new List<ElementHelper>();

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            return new OnInitOutputArgs();
        }

        public GQIColumn[] GetColumns()
        {
            return new GQIColumn[]
            {
                new GQIStringColumn("ID"),
                new GQIStringColumn("Label"),
                new GQIStringColumn("Status"),
                new GQIStringColumn("SRT Mode"),
                new GQIStringColumn("Caller Address"),
                new GQIStringColumn("Caller Source Port"),
                new GQIStringColumn("Caller Destination Port"),
                new GQIStringColumn("Listener Port"),
                new GQIStringColumn("Element Name"),
                new GQIStringColumn("Source Connected Label"),
                new GQIStringColumn("Routing Mode"),
                new GQIBooleanColumn("Is Selectable"),
            };
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[] { selectedElementArg, routingModeArg, siteLocationeArg, srtModeArg };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            args.TryGetArgumentValue(routingModeArg, out _routingMode);
            args.TryGetArgumentValue(siteLocationeArg, out _siteLocation);
            args.TryGetArgumentValue(srtModeArg, out _srtMode);
            args.TryGetArgumentValue(selectedElementArg, out _selectedElement);

            GetElementInfo();

            return new OnArgumentsProcessedOutputArgs();
        }

        private void GetElementInfo()
        {
            var siteFilter = !_siteLocation.IsNullOrEmpty() ? $"{_siteLocation}" : "Appear X Platform";

            var appearTVRequest = new GetLiteElementInfo
            {
                ProtocolName = "Appear X Platform",
                ProtocolVersion = "Production",
                View = siteFilter,
            };

            var appearMessage = _dms.SendMessages(new DMSMessage[] { appearTVRequest });
            foreach (var response in appearMessage.Select(x => (LiteElementInfoEvent)x))
            {
                if (response == null || response.State != ElementState.Active)
                {
                    continue;
                }

                elementHelperList.Add(new ElementHelper(response, _dms, _routingMode));
            }
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            var rows = new List<GQIRow>();
            try
            {
                foreach (var element in elementHelperList)
                {
                    GetRows(element, rows);
                }

                return new GQIPage(rows.ToArray())
                {
                    HasNextPage = false,
                };
            }
            catch (Exception ex)
            {
                rows.Add(CreateDebugRow($"{ex.Message}"));
                return new GQIPage(rows.ToArray())
                {
                    HasNextPage = false,
                };
            }
        }

        private void GetRows(ElementHelper element, List<GQIRow> rows)
        {
            if (_routingMode.Contains("SRT"))
            {
                GetDestinationSrtRows(element, rows);
            }
            else if (_routingMode.Contains("IP"))
            {
                GetDestinationIpRows(element, rows);
            }
            else
            {
                throw new Exception("Routing Mode not supported.");
            }
        }

        private void GetDestinationIpRows(ElementHelper element, List<GQIRow> rows)
        {
            GQICell[] cells;
            var possibleSources = elementHelperList.Where(x => x.ElementName != element.ElementName).ToList();
            for (int i = 0; i < element.IpInputsTable.Length; i++)
            {
                var destinationTableRow = element.IpInputsTable[i];
                var index = Convert.ToString(destinationTableRow[0]);
                var label = CheckExceptionValue(destinationTableRow[1 /*label*/]);
                var intStatus = Convert.ToInt32(destinationTableRow[2 /*Status*/]);
                var type = Convert.ToString(destinationTableRow[3 /*Type*/]);

                if (type == "seamless")
                {
                    continue;
                }

                var singleDestinationAddress = CheckExceptionValue(destinationTableRow[5 /*Single Destination Address*/]);
                var singleDestinationPort = CheckExceptionValue(destinationTableRow[6 /*Single Destination Port*/]);

                string status;
                if (!StateDict.TryGetValue(intStatus, out status))
                {
                    status = "N/A";
                }

                var isSelectable = true;
                if (_selectedElement == element.ElementName)
                {
                    isSelectable = false;
                }

                var sourceLabelName = "No Connection";
                if (status == "Enabled")
                {
                    sourceLabelName = FindSourceIPConnection(possibleSources, destinationTableRow);
                }

                cells = new[]
                {
                     new GQICell { Value = index },
                     new GQICell { Value = label },
                     new GQICell { Value = status },
                     new GQICell { Value = "N/A" },
                     new GQICell { Value = singleDestinationAddress },
                     new GQICell { Value = singleDestinationPort},
                     new GQICell { Value = "N/A" },
                     new GQICell { Value = "N/A" },
                     new GQICell { Value = element.ElementName },
                     new GQICell { Value = sourceLabelName },
                     new GQICell { Value = "IP" },
                     new GQICell { Value = isSelectable},
                };

                var elementID = new ElementID(element.Response.DataMinerID, element.Response.ElementID);
                var elementMetadata = new ObjectRefMetadata { Object = elementID };
                var rowMetadata = new GenIfRowMetadata(new[] { elementMetadata });
                var row = new GQIRow(cells) { Metadata = rowMetadata };

                rows.Add(row);
            }
        }

        private void GetDestinationSrtRows(ElementHelper element, List<GQIRow> rows)
        {
            GQICell[] cells;
            var possibleSources = elementHelperList.Where(x => x.ElementName != element.ElementName).ToList();
            for (int i = 0; i < element.SrtInputsTable.Length; i++)
            {
                var destinationTableRow = element.SrtInputsTable[i];
                var index = Convert.ToString(destinationTableRow[0]);
                var label = Convert.ToString(destinationTableRow[2 /*label*/]);
                var intStatus = Convert.ToInt32(destinationTableRow[3 /*Status*/]);
                var pathMode = Convert.ToString(destinationTableRow[7 /*Path Mode*/]);
                var pathCallerAddess = CheckExceptionValue(destinationTableRow[8 /*path 1 Caller Address*/]);
                var pathCallerSourcePort = CheckExceptionValue(destinationTableRow[9 /*path 1 Caller Source Port*/]);
                var pathCallerDestinationPort = CheckExceptionValue(destinationTableRow[10 /*path 1 Caller Destination Port*/]);
                var pathListenerPort = CheckExceptionValue(destinationTableRow[11 /*path 1 Caller Listener Port*/]);

                bool isSelectable = true;
                if (_srtMode == "LISTENER" && pathMode == "LISTENER")
                {
                    isSelectable = false;
                }
                else if (_srtMode == "CALLER" && pathMode == "CALLER")
                {
                    isSelectable = false;
                }
                else if (_selectedElement == element.ElementName)
                {
                    isSelectable = false;
                }
                else
                {
                    // No Filter Action
                }

                string status;
                if (!StateDict.TryGetValue(intStatus, out status))
                {
                    status = "N/A";
                }

                var sourceLabelName = "No Connection";
                if (status == "Enabled" && pathMode == "CALLER")
                {
                    sourceLabelName = FindSourceConnection(possibleSources, destinationTableRow);
                }
                else if (pathMode == "LISTENER")
                {
                    pathCallerAddess = CheckExceptionValue(destinationTableRow[30 /*path interface IP*/]);
                    pathCallerSourcePort = pathListenerPort;
                }
                else
                {
                    // No action
                }

                cells = new[]
                {
                     new GQICell { Value = index },
                     new GQICell { Value = label },
                     new GQICell { Value = status },
                     new GQICell { Value = pathMode},
                     new GQICell { Value = pathCallerAddess},
                     new GQICell { Value = pathCallerSourcePort },
                     new GQICell { Value = pathCallerDestinationPort },
                     new GQICell { Value = pathListenerPort },
                     new GQICell { Value = element.ElementName},
                     new GQICell { Value = sourceLabelName},
                     new GQICell { Value = "SRT" },
                     new GQICell { Value = isSelectable},
                };

                var elementID = new ElementID(element.Response.DataMinerID, element.Response.ElementID);
                var elementMetadata = new ObjectRefMetadata { Object = elementID };
                var rowMetadata = new GenIfRowMetadata(new[] { elementMetadata });
                var row = new GQIRow(cells) { Metadata = rowMetadata };

                rows.Add(row);
            }
        }

        private string FindSourceConnection(List<ElementHelper> possibleSources, object[] destinationTableRow)
        {
            if (!possibleSources.Any())
            {
                return "No Connection";
            }

            foreach (var possibleSource in possibleSources.Select(x => x.SrtOutputsTable))
            {
                var sourcesTable = possibleSource;

                var pathMode = Convert.ToString(destinationTableRow[7 /*Path Mode*/]);
                if (pathMode == "CALLER")
                {
                    for (int i = 0; i < sourcesTable.Length; i++)
                    {
                        var sourceRow = sourcesTable[i];
                        var intStatus = Convert.ToInt32(sourceRow[3 /*Status*/]);
                        var pathSourceMode = Convert.ToString(sourceRow[6 /*Path Mode*/]);

                        if (pathSourceMode != "LISTENER" || intStatus != 1 /*Enabled*/)
                        {
                            continue;
                        }

                        var pathCallerAddress = CheckExceptionValue(destinationTableRow[8 /*path 1 Caller Address*/]);
                        var pathInterfaceIP = CheckExceptionValue(sourceRow[31 /*path interface IP*/]);

                        var pathCallerSourcePort = CheckExceptionValue(destinationTableRow[9 /*path 1 Caller Source Port*/]);
                        var pathListenerSourcePort = CheckExceptionValue(sourceRow[10 /*path 1 Listener Source Port*/]);

                        if (pathCallerSourcePort == pathListenerSourcePort && pathCallerAddress == pathInterfaceIP)
                        {
                            return Convert.ToString(sourceRow[2]);
                        }
                    }
                }
                else
                {
                    // Not supported action
                }
            }

            return "No Connection";
        }

        private string FindSourceIPConnection(List<ElementHelper> possibleSources, object[] destinationTableRow)
        {
            if (!possibleSources.Any())
            {
                return "No Connection";
            }

            var singleDestinationAddress = CheckExceptionValue(destinationTableRow[5 /*Single Destination Address*/]);
            var singleDestinationPort = CheckExceptionValue(destinationTableRow[6 /*Single Destination Port*/]);

            if (singleDestinationAddress.Equals("N/A") || singleDestinationPort.Equals("N/A"))
            {
                return "No Connection";
            }

            foreach (var possibleSource in possibleSources.Select(x => x.IpOutputsTable))
            {
                var sourcesTable = possibleSource;

                for (int i = 0; i < sourcesTable.Length; i++)
                {
                    var sourceRow = sourcesTable[i];
                    var intStatus = Convert.ToInt32(sourceRow[2 /*Status*/]);

                    if (intStatus != 1 /*Enabled*/)
                    {
                        continue;
                    }

                    var sourceRowDestinationAddress = CheckExceptionValue(sourceRow[5 /*Single Destination Address*/]);
                    var sourceRowDestinationPort = CheckExceptionValue(sourceRow[6 /*Single Destination Port*/]);

                    if ((sourceRowDestinationAddress == singleDestinationAddress) && (sourceRowDestinationPort == singleDestinationPort))
                    {
                        return Convert.ToString(sourceRow[1]);
                    }
                }
            }

            return "No Connection";
        }

        private GQIRow CreateDebugRow(string message)
        {
            var cells = new[]
            {
                     new GQICell { Value = message },
                     new GQICell {},
                     new GQICell {},
                     new GQICell {},
                     new GQICell {},
                     new GQICell {},
                     new GQICell {},
                     new GQICell {},
                     new GQICell {},
                     new GQICell {},
                     new GQICell {},
            };

            var row = new GQIRow(cells);

            return row;
        }

        private string CheckExceptionValue(object value)
        {
            var sValue = Convert.ToString(value);

            if (sValue.IsNullOrEmpty())
            {
                return "N/A";
            }

            if (exceptionsDict.TryGetValue(sValue, out string exceptionValue))
            {
                return exceptionValue;
            }

            return sValue;
        }
    }

    public class ElementHelper
    {
        private readonly GQIDMS dms;
        private readonly string routingMode;

        public ElementHelper(LiteElementInfoEvent response, GQIDMS dms, string routingMode)
        {
            Response = response;
            this.routingMode = routingMode;
            this.dms = dms;

            ElementName = response.Name;
            GetTables(response);
        }

        public LiteElementInfoEvent Response { get; set; }

        public string ElementName { get; set; }

        public object[][] SrtOutputsTable { get; set; }

        public object[][] SrtInputsTable { get; set; }

        public object[][] IpOutputsTable { get; set; }

        public object[][] IpInputsTable { get; set; }

        private static object[][] GetTable(GQIDMS dms, LiteElementInfoEvent response, int tableId)
        {
            var partialTableRequest = new GetPartialTableMessage
            {
                DataMinerID = response.DataMinerID,
                ElementID = response.ElementID,
                ParameterID = tableId,
            };

            var messageResponse = dms.SendMessage(partialTableRequest) as ParameterChangeEventMessage;
            if (messageResponse.NewValue.ArrayValue != null && messageResponse.NewValue.ArrayValue.Length > 0)
            {
                return BuildRows(messageResponse.NewValue.ArrayValue);
            }
            else
            {
                return new object[0][];
            }
        }

        private static object[][] BuildRows(ParameterValue[] columns)
        {
            int length1 = columns.Length;
            int length2 = 0;
            if (length1 > 0)
                length2 = columns[0].ArrayValue.Length;
            object[][] objArray;
            if (length1 > 0 && length2 > 0)
            {
                objArray = new object[length2][];
                for (int index = 0; index < length2; ++index)
                    objArray[index] = new object[length1];
            }
            else
            {
                objArray = new object[0][];
            }

            for (int index1 = 0; index1 < length1; ++index1)
            {
                ParameterValue[] arrayValue = columns[index1].ArrayValue;
                for (int index2 = 0; index2 < length2; ++index2)
                    objArray[index2][index1] = arrayValue[index2].IsEmpty ? (object)null : arrayValue[index2].ArrayValue[0].InteropValue;
            }

            return objArray;
        }

        private void GetTables(LiteElementInfoEvent response)
        {
            if (routingMode.Contains("SRT"))
            {
                SrtInputsTable = GetTable(dms, response, 14000 /*SRT Inputs*/);
                SrtOutputsTable = GetTable(dms, response, 12000 /*SRT Outputs*/);
            }
            else if (routingMode.Contains("IP"))
            {
                IpInputsTable = GetTable(dms, response, 1500 /*IP Inputs*/);
                IpOutputsTable = GetTable(dms, response, 1600 /*IP Outputs*/);
            }
            else
            {
                // No Action
            }
        }
    }
}