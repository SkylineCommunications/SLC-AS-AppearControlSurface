namespace IAS_Switching_Connect_1
{
    using System;
    using System.Linq;
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

            if (srcSrtMode == "Caller")
            {
                var listenerCardIp = GetIpAddress(dstDmsElement, dstRow);
                var listenerPort = Convert.ToInt32(dstRow[10]);

                if (String.IsNullOrWhiteSpace(listenerCardIp))
                {
                    ErrorMessageDialog.ShowMessage(engine, $"IP Address not found on Listener (Destination).");
                    return;
                }

                // Caller Sets (IP Address and Port)
                srcElement.SetParameterByPrimaryKey(12058, sourceId, listenerCardIp);
                srcElement.SetParameterByPrimaryKey(14062, sourceId, listenerPort);
            }
            else
            {
                var listenerCardIp = GetIpAddress(srcDmsElement, srcRow);
                var listenerPort = Convert.ToInt32(srcRow[10]);

                if (String.IsNullOrWhiteSpace(listenerCardIp))
                {
                    ErrorMessageDialog.ShowMessage(engine, $"IP Address not found on Listener (Source).");
                    return;
                }

                // Caller Sets (IP Address and Port)
                dstElement.SetParameterByPrimaryKey(14059, destinationId, listenerCardIp);
                dstElement.SetParameterByPrimaryKey(14060, destinationId, listenerPort);
            }

            var srcStatus = Convert.ToInt32(srcRow[3 /*Status*/]);
            var dstStatus = Convert.ToInt32(dstRow[3 /*Status*/]);
            EnableRows(sourceId, destinationId, srcStatus, dstStatus);
        }

        private void EnableRows(string sourceId, string destinationId, int srcStatus, int dstStatus)
        {
            var srcDisabled = srcStatus == (int)Status.Disabled;
            var dstDisabled = dstStatus == (int)Status.Disabled;

            if (srcDisabled)
            {
                srcElement.SetParameterByPrimaryKey(12054, sourceId, (int)Status.Enabled);
            }

            if (dstDisabled)
            {
                dstElement.SetParameterByPrimaryKey(14054, destinationId, (int)Status.Enabled);
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

        private string GetIpAddress(IDmsElement dmsElement, object[] srcRow)
        {
            var srcSlot = Convert.ToInt32(srcRow[1]);
            var srcInterfaceName = Convert.ToString(srcRow[29]);

            var ipInterfacesTableData = dmsElement.GetTable(IpInterfacesTable).GetData();
            foreach (var interfaceRow in ipInterfacesTableData.Values)
            {
                var interfaceSlot = Convert.ToInt32(interfaceRow[10]);
                var interfaceName = Convert.ToString(interfaceRow[3]);

                if (interfaceSlot == srcSlot && interfaceName == srcInterfaceName)
                {
                    return Convert.ToString(interfaceRow[5] /*IP Address*/);
                }
            }

            return String.Empty;
        }
    }
}
