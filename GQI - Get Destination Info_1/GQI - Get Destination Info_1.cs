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

namespace GQI_GetDestinationInfo
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Helper;
    using Skyline.DataMiner.Net.Messages;

    [GQIMetaData(Name = "GQI - Get Destination Info")]
    public sealed class GetDestinationInfo : IGQIDataSource, IGQIOnInit, IGQIInputArguments
    {
        private readonly GQIStringArgument routingModeArg = new GQIStringArgument("Routing Mode") { IsRequired = true };
        private readonly GQIStringArgument elementNameArg = new GQIStringArgument("Element Name") { IsRequired = true };
        private readonly GQIStringArgument sourceIdArg = new GQIStringArgument("Source ID") { IsRequired = true };

        private readonly Dictionary<string, string> exceptionsDict = new Dictionary<string, string>
        {
            {"-1","N/A"},
            {"-2", "N/A" },
        };

        private readonly Dictionary<string, string> SocketStateDict = new Dictionary<string, string>
        {
            { "SRT_INIT", "Initializing" },
            { "SRT_OPENED", "Socket Opened" },
            { "SRT_LISTENING", "Listening" },
            { "SRT_CONNECTING", "Connecting" },
            { "SRT_CONNECTED", "Connected" },
            { "SRT_BROKEN", "Broken" },
            { "SRT_CLOSING", "Closing" },
            { "SRT_CLOSED", "Closed" },
            { "SRT_NONEXIST", "Non-Existent" },
        };

        private string _elementName;
        private string _sourceId;
        private string _routingMode;
        private GQIDMS _dms;

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
                new GQIStringColumn("Socket State"),
                new GQIDoubleColumn("Total Bitrate"),
                new GQIDoubleColumn("Retransmitted Bitrate"),
            };
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[] { elementNameArg, sourceIdArg, routingModeArg };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            args.TryGetArgumentValue(elementNameArg, out _elementName);
            args.TryGetArgumentValue(sourceIdArg, out _sourceId);
            args.TryGetArgumentValue(routingModeArg, out _routingMode);
            return new OnArgumentsProcessedOutputArgs();
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            var rows = new List<GQIRow>();
            try
            {
                var appearTVRequest = new GetLiteElementInfo
                {
                    NameFilter = _elementName,
                    ProtocolName = "Appear X Platform",
                    ProtocolVersion = "Production",
                };

                var appearTvResponses = _dms.SendMessages(new DMSMessage[] { appearTVRequest });

                foreach (var response in appearTvResponses.Select(x => (LiteElementInfoEvent)x))
                {
                    if (response == null || response.State != ElementState.Active)
                    {
                        continue;
                    }

                    object[][] destinationsStatusTable;

                    if (_routingMode.Contains("SRT"))
                    {
                        destinationsStatusTable = GetTable(_dms, response, 11200 /*SRT Inputs Status*/);
                        GetDestinationSrtRows(response, rows, destinationsStatusTable);
                    }
                    else if (_routingMode.Contains("IP"))
                    {
                        destinationsStatusTable = GetTable(_dms, response, 2100 /*IP Inputs Status*/);
                        GetDestinationIPRows(response, rows, destinationsStatusTable);
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

        private void GetDestinationIPRows(LiteElementInfoEvent response, List<GQIRow> rows, object[][] destinationsStatusTable)
        {
            GQICell[] cells;
            for (int i = 0; i < destinationsStatusTable.Length; i++)
            {
                var destinationStatusTableRow = destinationsStatusTable[i];
                var index = Convert.ToString(destinationStatusTableRow[0]);

                if (index != _sourceId)
                {
                    continue;
                }

                var totalBitrate = Convert.ToDouble(destinationStatusTableRow[1]);
                totalBitrate = Math.Round(totalBitrate, 3);
                var sTotalBitrate = $"{totalBitrate} Mbps";
                if (totalBitrate < 0)
                {
                    sTotalBitrate = "N/A";
                }

                cells = new[]
                {
                     new GQICell { Value = index },
                     new GQICell { Value = "N/A" },
                     new GQICell { Value = totalBitrate, DisplayValue = sTotalBitrate},
                     new GQICell { Value = 0.0, DisplayValue = "N/A"},
                };

                var elementID = new ElementID(response.DataMinerID, response.ElementID);
                var elementMetadata = new ObjectRefMetadata { Object = elementID };
                var rowMetadata = new GenIfRowMetadata(new[] { elementMetadata });
                var row = new GQIRow(cells) { Metadata = rowMetadata };

                rows.Add(row);
            }
        }

        private void GetDestinationSrtRows(LiteElementInfoEvent response, List<GQIRow> rows, object[][] destinationsStatusTable)
        {
            GQICell[] cells;
            for (int i = 0; i < destinationsStatusTable.Length; i++)
            {
                var destinationStatusTableRow = destinationsStatusTable[i];
                var index = Convert.ToString(destinationStatusTableRow[0]);

                if (index != _sourceId)
                {
                    continue;
                }

                var socketState = CheckExceptionValue(destinationStatusTableRow[10]);
                var totalBitrate = Convert.ToDouble(destinationStatusTableRow[16]);
                var retransmittedBitrate = Convert.ToDouble(destinationStatusTableRow[17]);

                string socketValue;

                if (!SocketStateDict.TryGetValue(socketState, out socketValue))
                {
                    socketValue = "N/A";
                }

                totalBitrate = Math.Round(totalBitrate, 3);
                retransmittedBitrate = Math.Round(retransmittedBitrate, 3);

                var sTotalBitrate = $"{totalBitrate} Mbps";
                if (totalBitrate < 0)
                {
                    sTotalBitrate = "N/A";
                }

                var sRetransmittedBitrate = $"{retransmittedBitrate} Mbps";
                if (retransmittedBitrate < 0)
                {
                    sRetransmittedBitrate = "N/A";
                }


                cells = new[]
                {
                     new GQICell { Value = index },
                     new GQICell { Value = socketValue },
                     new GQICell { Value = totalBitrate, DisplayValue = sTotalBitrate},
                     new GQICell { Value = retransmittedBitrate, DisplayValue = sRetransmittedBitrate},
                };

                var elementID = new ElementID(response.DataMinerID, response.ElementID);
                var elementMetadata = new ObjectRefMetadata { Object = elementID };
                var rowMetadata = new GenIfRowMetadata(new[] { elementMetadata });
                var row = new GQIRow(cells) { Metadata = rowMetadata };

                rows.Add(row);
            }
        }

        private GQIRow CreateDebugRow(string message)
        {
            var cells = new[]
            {
                     new GQICell { Value = message },
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