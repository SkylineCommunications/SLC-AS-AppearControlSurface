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

namespace GQI_GetSources
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Helper;
    using Skyline.DataMiner.Net.Messages;

    [GQIMetaData(Name = "GQI - Get Sources")]
    public sealed class GetSource : IGQIDataSource, IGQIOnInit, IGQIInputArguments
    {
        private readonly GQIStringArgument selectedElementArg = new GQIStringArgument("Selected Destination Element Name") { IsRequired = false, DefaultValue = string.Empty };
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
        private List<LiteElementInfoEvent> appearTvResponses;

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
                new GQIStringColumn("Path Mode"),
                new GQIStringColumn("Path Caller Address"),
                new GQIStringColumn("Path Caller Source Port"),
                new GQIStringColumn("Path Caller Destination Port"),
                new GQIStringColumn("Path Listener Port"),
                new GQIStringColumn("Element Name"),
                new GQIStringColumn("Routing Mode"),
                new GQIStringColumn("Destination Connected Label"),
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
            return new OnArgumentsProcessedOutputArgs();
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            var rows = new List<GQIRow>();
            try
            {
                var siteFilter = !_siteLocation.IsNullOrEmpty() ? $"{_siteLocation}" : "Appear X Platform";

                var appearTVRequest = new GetLiteElementInfo
                {
                    ProtocolName = "Appear X Platform",
                    ProtocolVersion = "Production",
                    View = siteFilter,
                };

                var appearMessage = _dms.SendMessages(new DMSMessage[] { appearTVRequest });
                appearTvResponses = appearMessage.Select(x => (LiteElementInfoEvent)x).ToList();
                foreach (var response in appearTvResponses)
                {
                    if (response == null || response.State != ElementState.Active)
                    {
                        continue;
                    }

                    object[][] sourcesTable;

                    if (_routingMode.Contains("SRT"))
                    {
                        sourcesTable = GetTable(_dms, response, 12000 /*SRT Outputs*/);
                        GetSourceSrtRows(response, rows, sourcesTable);
                    }
                    else if (_routingMode.Contains("IP"))
                    {
                        sourcesTable = GetTable(_dms, response, 1600 /*IP Outputs*/);
                        GetSourceIpRows(response, rows, sourcesTable);
                    }
                    else
                    {
                        throw new Exception("Routing Mode not supported.");
                    }
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

        private void GetSourceIpRows(LiteElementInfoEvent response, List<GQIRow> rows, object[][] sourcesTable)
        {
            GQICell[] cells;

            // Sources - Outputs
            for (int i = 0; i < sourcesTable.Length; i++)
            {
                var sourceTableRow = sourcesTable[i];
                var index = Convert.ToString(sourceTableRow[0]);
                var label = CheckExceptionValue(sourceTableRow[1 /*label*/]);
                var intStatus = Convert.ToInt32(sourceTableRow[2 /*Status*/]);

                var singleDestinationAddress = CheckExceptionValue(sourceTableRow[5 /*Single Destination Address*/]);
                var singleDestinationPort = CheckExceptionValue(sourceTableRow[6 /*Single Destination Port*/]);

                string status;
                if (!StateDict.TryGetValue(intStatus, out status))
                {
                    status = "N/A";
                }

                var isSelectable = true;
                if (_selectedElement == response.Name)
                {
                    isSelectable = false;
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
                     new GQICell { Value = response.Name },
                     new GQICell { Value = "IP" },
                     new GQICell { Value = "N/A" },
                     new GQICell { Value = isSelectable},
                };

                var elementID = new ElementID(response.DataMinerID, response.ElementID);
                var elementMetadata = new ObjectRefMetadata { Object = elementID };
                var rowMetadata = new GenIfRowMetadata(new[] { elementMetadata });
                var row = new GQIRow(cells) { Metadata = rowMetadata };

                rows.Add(row);
            }
        }

        private void GetSourceSrtRows(LiteElementInfoEvent response, List<GQIRow> rows, object[][] sourcesTable)
        {
            GQICell[] cells;

            // Sources - Outputs
            var possibleDestinations = appearTvResponses.Where(x => (x.Name != response.Name) && (x.State == ElementState.Active)).ToList();
            for (int i = 0; i < sourcesTable.Length; i++)
            {
                var sourceTableRow = sourcesTable[i];
                var index = Convert.ToString(sourceTableRow[0]);
                var label = Convert.ToString(sourceTableRow[2 /*label*/]);
                var intStatus = Convert.ToInt32(sourceTableRow[3 /*Status*/]);
                var pathMode = Convert.ToString(sourceTableRow[6 /*Path Mode*/]);
                var pathCallerAddess = CheckExceptionValue(sourceTableRow[7 /*path 1 Caller Address*/]);
                var pathCallerSourcePort = CheckExceptionValue(sourceTableRow[8 /*path 1 Caller Source Port*/]);
                var pathCallerDestinationPort = CheckExceptionValue(sourceTableRow[9 /*path 1 Caller Destination Port*/]);
                var pathListenerPort = CheckExceptionValue(sourceTableRow[10 /*path 1 Caller Listener Port*/]);

                string status;
                if (!StateDict.TryGetValue(intStatus, out status))
                {
                    status = "N/A";
                }

                bool isSelectable = true;
                if (_srtMode == "LISTENER" && pathMode == "LISTENER")
                {
                    isSelectable = false;
                }
                else if (_srtMode == "CALLER" && pathMode == "CALLER")
                {
                    isSelectable = false;
                }
                else if (_selectedElement == response.Name)
                {
                    isSelectable = false;
                }
                else
                {
                    // No Filter Action
                }

                var sourceLabelName = "No Connection";
                if (status == "Enabled" && pathMode == "CALLER")
                {
                    sourceLabelName = FindSourceConnection(possibleDestinations, sourceTableRow);
                }
                else if (pathMode == "LISTENER")
                {
                    pathCallerAddess = CheckExceptionValue(sourceTableRow[31 /*path interface IP*/]);
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
                     new GQICell { Value = response.Name},
                     new GQICell { Value = "SRT"},
                     new GQICell { Value = sourceLabelName },
                     new GQICell { Value = isSelectable},
                };

                var elementID = new ElementID(response.DataMinerID, response.ElementID);
                var elementMetadata = new ObjectRefMetadata { Object = elementID };
                var rowMetadata = new GenIfRowMetadata(new[] { elementMetadata });
                var row = new GQIRow(cells) { Metadata = rowMetadata };

                rows.Add(row);
            }
        }

        private string FindSourceConnection(List<LiteElementInfoEvent> possibleSources, object[] sourceTableRow)
        {
            if (!possibleSources.Any())
            {
                return "No Connection";
            }

            foreach (var possibleSource in possibleSources)
            {
                // if pathMode is listener, need to find the source srt that have the sourcePort == ListenerPort, get the caller Address, go back to destination Element and in dual IP table find the matching IP
                // For now will only compare the ports

                // if pathMode is caller, need to grab the caller source port from destination element, go to the dual ip interfaces table, find the matching IP, get the slot and then find the matching row by slot and sourcePort == ListenerPort

                var destinationTable = GetTable(_dms, possibleSource, 14000 /*SRT Outputs*/);

                var pathMode = Convert.ToString(sourceTableRow[6 /*Path Mode*/]);
                if (pathMode == "CALLER")
                {
                    for (int i = 0; i < destinationTable.Length; i++)
                    {
                        var destinationRow = destinationTable[i];
                        var intStatus = Convert.ToInt32(destinationRow[3 /*Status*/]);
                        var pathSourceMode = Convert.ToString(destinationRow[7 /*Path Mode*/]);

                        if (pathSourceMode != "LISTENER" || intStatus != 1 /*Enabled*/)
                        {
                            continue;
                        }

                        var pathCallerAddress = CheckExceptionValue(sourceTableRow[7 /*path 1 Caller Address*/]);
                        var pathInterfaceIP = CheckExceptionValue(destinationRow[30 /*path interface IP*/]);

                        var pathListenerSourcePort = CheckExceptionValue(destinationRow[11 /*path 1 Listener Source Port*/]);
                        var pathCallerSourcePort = CheckExceptionValue(sourceTableRow[8 /*path 1 Caller Source Port*/]);

                        if (pathCallerSourcePort == pathListenerSourcePort && pathCallerAddress == pathInterfaceIP)
                        {
                            return Convert.ToString(destinationRow[2]);
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

        public static object[][] GetTable(GQIDMS dms, LiteElementInfoEvent response, int tableId)
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
    }
}