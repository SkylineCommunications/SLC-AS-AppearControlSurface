﻿namespace IAS_Switching_Connect_1
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Core.DataMinerSystem.Automation;
    using Skyline.DataMiner.Core.DataMinerSystem.Common;

    internal class SRT
    {
        private const int InputsTable = 14000;
        private const int OutputsTable = 12000;
        private const int IpInterfacesTable = 2000;

        private readonly string sourceId;
        private readonly string destinationId;
        private readonly string sourceElement;
        private readonly string destinationElement;
        private readonly IEngine engine;

        private IDmsElement srcDmsElement;
        private IDmsElement dstDmsElement;
        private Element srcElement;
        private Element dstElement;
        private IDmsTable srcTable;
        private IDmsTable dstTable;

        public SRT(IEngine engine, string sourceId, string destinationId, string sourceElement, string destinationElement)
        {
            this.engine = engine;
            this.sourceId = sourceId;
            this.destinationId = destinationId;
            this.sourceElement = sourceElement;
            this.destinationElement = destinationElement;

            InitializeGlobalVariables(engine);
        }

        private enum Status
        {
            Disabled = 0,
            Enabled = 1,
        }

        public void StartConnection()
        {
            if (!ValidateKeysExists())
            {
                return;
            }

            var srcRow = srcTable.GetRow(sourceId);
            var dstRow = dstTable.GetRow(destinationId);

            if (!ValidateRowExist(srcRow, dstRow))
            {
                return;
            }

            var srcSrtMode = Convert.ToString(srcRow[6] /*SRT Mode*/);
            var dstSrtMode = Convert.ToString(dstRow[7]/*SRT Mode*/);

            if (srcSrtMode == dstSrtMode)
            {
                var message = $"Selected Source and Destination have the same Mode ({srcSrtMode}). Please select one Caller and one Listener.";
                ErrorMessageDialog.ShowMessage(engine, message);
                return;
            }

            if (srcSrtMode == "CALLER")
            {
                var listenerCardIp = GetIpAddress(dstDmsElement, dstRow, false);
                var listenerPort = Convert.ToInt32(dstRow[11]);

                if (String.IsNullOrWhiteSpace(listenerCardIp))
                {
                    ErrorMessageDialog.ShowMessage(engine, $"IP Address not found on Listener (Destination).");
                    return;
                }

                // Caller Sets (IP Address and Port)
                srcElement.SetParameterByPrimaryKey(12058, sourceId, listenerCardIp);
                Thread.Sleep(2000);
                srcElement.SetParameterByPrimaryKey(12059, sourceId, listenerPort);
                Thread.Sleep(2000);
                srcElement.SetParameterByPrimaryKey(12060, sourceId, listenerPort);
                Thread.Sleep(2000);

                if (!Retry(ValidateSets, new TimeSpan(0, 1, 0), listenerCardIp, listenerPort, true))
                {
                    ErrorMessageDialog.ShowMessage(engine, $"Listener IP Address and/or Port were not set on Caller.");
                    return;
                }
            }
            else
            {
                var listenerCardIp = GetIpAddress(srcDmsElement, srcRow, true);
                var listenerPort = Convert.ToInt32(srcRow[10]);

                if (String.IsNullOrWhiteSpace(listenerCardIp))
                {
                    ErrorMessageDialog.ShowMessage(engine, $"IP Address not found on Listener (Source).");
                    return;
                }

                // Caller Sets (IP Address and Port)
                dstElement.SetParameterByPrimaryKey(14059, destinationId, listenerCardIp);
                Thread.Sleep(2000);
                dstElement.SetParameterByPrimaryKey(14060, destinationId, listenerPort);
                Thread.Sleep(2000);
                dstElement.SetParameterByPrimaryKey(14061, destinationId, listenerPort);
                Thread.Sleep(2000);

                if (!Retry(ValidateSets, new TimeSpan(0, 1, 0), listenerCardIp, listenerPort, false))
                {
                    ErrorMessageDialog.ShowMessage(engine, $"Listener IP Address and/or Port were not set on Caller.");
                    return;
                }
            }

            var srcStatus = Convert.ToInt32(srcRow[3 /*Status*/]);
            var dstStatus = Convert.ToInt32(dstRow[3 /*Status*/]);
            EnableRows(sourceId, destinationId, srcStatus, dstStatus);
        }

        private bool ValidateSets(string listenerCardIp, int listenerPort, bool callerOnSourceSide)
        {
            if (callerOnSourceSide)
            {
                var row = srcDmsElement.GetTable(OutputsTable).GetRow(sourceId);

                return Convert.ToString(row[7]) == listenerCardIp && Convert.ToInt32(row[8]) == listenerPort && Convert.ToInt32(row[9]) == listenerPort;
            }
            else
            {
                var row = dstDmsElement.GetTable(InputsTable).GetRow(destinationId);
                return Convert.ToString(row[8]) == listenerCardIp && Convert.ToInt32(row[9]) == listenerPort && Convert.ToInt32(row[10]) == listenerPort;
            }
        }

        private bool ValidateStatus()
        {
            var srcRow = srcDmsElement.GetTable(OutputsTable).GetRow(sourceId);
            var dstRow = dstDmsElement.GetTable(InputsTable).GetRow(destinationId);

            return Convert.ToInt32(srcRow[3]) == (int)Status.Enabled && Convert.ToInt32(dstRow[3]) == (int)Status.Enabled;
        }

        private void EnableRows(string sourceId, string destinationId, int srcStatus, int dstStatus)
        {
            var srcDisabled = srcStatus == (int)Status.Disabled;
            var dstDisabled = dstStatus == (int)Status.Disabled;

            if (srcDisabled)
            {
                srcElement.SetParameterByPrimaryKey(12054, sourceId, (int)Status.Enabled);
                Thread.Sleep(2000);
            }

            if (dstDisabled)
            {
                dstElement.SetParameterByPrimaryKey(14054, destinationId, (int)Status.Enabled);
                Thread.Sleep(2000);
            }

            if (!Retry(ValidateStatus, new TimeSpan(0, 1, 0)))
            {
                ErrorMessageDialog.ShowMessage(engine, $"Source/Destination not enabled.");
            }
        }

        private void InitializeGlobalVariables(IEngine engine)
        {
            var dms = engine.GetDms();

            var srcElementSplitted = sourceElement.Split('/');
            var dstElementSplitted = destinationElement.Split('/');

            if (srcElementSplitted.Length < 2 || dstElementSplitted.Length < 2)
            {
                ErrorMessageDialog.ShowMessage(engine, $"Element parameters don't match with expected data. Format: [dmaId/elementId]");
                return;
            }

            var srcElementId = new DmsElementId(Convert.ToInt32(srcElementSplitted[0]), Convert.ToInt32(srcElementSplitted[1]));
            var dstElementId = new DmsElementId(Convert.ToInt32(dstElementSplitted[0]), Convert.ToInt32(dstElementSplitted[1]));

            srcDmsElement = dms.GetElement(srcElementId);
            dstDmsElement = dms.GetElement(dstElementId);
            srcElement = engine.FindElement(srcElementId.AgentId, srcElementId.ElementId);
            dstElement = engine.FindElement(dstElementId.AgentId, dstElementId.ElementId);

            srcTable = srcDmsElement.GetTable(OutputsTable);
            dstTable = dstDmsElement.GetTable(InputsTable);
        }

        private bool ValidateKeysExists()
        {
            var srcTableKeys = srcTable.GetPrimaryKeys();
            var dstTableKeys = dstTable.GetPrimaryKeys();

            if (!srcTableKeys.Contains(sourceId))
            {
                ErrorMessageDialog.ShowMessage(engine, $"Key not found on Source SRT Outputs table. Source key: {sourceId}");
                return false;
            }

            if (!dstTableKeys.Contains(destinationId))
            {
                ErrorMessageDialog.ShowMessage(engine, $"Key not found on Destination SRT Inputs table. Destination key: {destinationId}");
                return false;
            }

            return true;
        }

        private bool ValidateRowExist(object[] srcRow, object[] dstRow)
        {
            if (srcRow == null)
            {
                ErrorMessageDialog.ShowMessage(engine, $"Row not found on Source SRT Outputs table. Row key: {sourceId}");
                return false;
            }

            if (dstRow == null)
            {
                ErrorMessageDialog.ShowMessage(engine, $"Row not found on Destination SRT Inputs table. Row key: {destinationId}");
                return false;
            }

            return true;
        }

        private string GetIpAddress(IDmsElement dmsElement, object[] row, bool listenerOnSourceSide)
        {
            var srcSlot = Convert.ToInt32(row[1]);
            var srcInterfaceName = listenerOnSourceSide ? Convert.ToString(row[29]) : Convert.ToString(row[28]);

            var ipInterfacesTableData = dmsElement.GetTable(IpInterfacesTable).GetData();
            foreach (var interfaceRow in ipInterfacesTableData.Values)
            {
                var interfaceSlot = Convert.ToInt32(interfaceRow[11]);
                var interfaceName = Convert.ToString(interfaceRow[3]);

                if (interfaceSlot == srcSlot && interfaceName == srcInterfaceName)
                {
                    return Convert.ToString(interfaceRow[5] /*IP Address*/);
                }
            }

            return String.Empty;
        }

        private bool Retry(Func<string, int, bool, bool> func, TimeSpan timeout, string listenerIP, int listenerPort, bool listenerOnSourceSide)
        {
            bool success;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            do
            {
                success = func(listenerIP, listenerPort, listenerOnSourceSide);
                if (!success)
                {
                    Thread.Sleep(3000);
                }
            }
            while (!success && sw.Elapsed <= timeout);

            return success;
        }

        private bool Retry(Func<bool> func, TimeSpan timeout)
        {
            bool success;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            do
            {
                success = func();
                if (!success)
                {
                    Thread.Sleep(3000);
                }
            }
            while (!success && sw.Elapsed <= timeout);

            return success;
        }
    }
}
