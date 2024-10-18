namespace IAS_Switching_Connect_1
{
    using System;
    using Library.Dialogs;
    using Library.SharedMethods;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Core.DataMinerSystem.Automation;
    using Skyline.DataMiner.Core.DataMinerSystem.Common;

    internal class IP
    {
        private const int InputsTable = 1500;
        private const int OutputsTable = 1600;

        private readonly string sourceId;
        private readonly string destinationId;
        private readonly string sourceElement;
        private readonly string destinationElement;
        private readonly IEngine engine;

        private Element srcElement;
        private Element dstElement;
        private IDmsTable srcTable;
        private IDmsTable dstTable;

        public IP(IEngine engine, string sourceId, string destinationId, string sourceElement, string destinationElement)
        {
            this.engine = engine;
            this.sourceId = sourceId;
            this.destinationId = destinationId;
            this.sourceElement = sourceElement;
            this.destinationElement = destinationElement;

            InitializeGlobalVariables(engine);
        }

        public void StartConnection()
        {
            var srcRow = srcTable.GetRow(sourceId);
            var dstRow = dstTable.GetRow(destinationId);

            if (srcRow == null)
            {
                ErrorMessageDialog.ShowMessage(engine, $"Row not found on IP Outputs table. Row key: {sourceId}");
                return;
            }

            if (dstRow == null)
            {
                ErrorMessageDialog.ShowMessage(engine, $"Row not found on IP Inputs table. Row key: {destinationId}");
                return;
            }

            var srcDisabled = Convert.ToInt32(srcRow[2 /*Status*/]) == (int)SharedMethods.Status.Disabled;
            var dstDisabled = Convert.ToInt32(dstRow[2 /*Status*/]) == (int)SharedMethods.Status.Disabled;

            var srcIpAddress = Convert.ToString(srcRow[5 /*Single Destination IP*/]);
            var srcPort = Convert.ToString(srcRow[6 /*Single Destination Port*/]);

            dstElement.SetParameterByPrimaryKey(1546, destinationId, srcIpAddress);
            dstElement.SetParameterByPrimaryKey(1547, destinationId, srcPort);

            if (srcDisabled)
            {
                srcElement.SetParameterByPrimaryKey(1643, sourceId, (int)SharedMethods.Status.Enabled);
            }

            if (dstDisabled)
            {
                dstElement.SetParameterByPrimaryKey(1543, destinationId, (int)SharedMethods.Status.Enabled);
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

            var srcDmsElement = dms.GetElement(srcElementId);
            var dstDmsElement = dms.GetElement(dstElementId);
            srcElement = engine.FindElement(srcElementId.AgentId, srcElementId.ElementId);
            dstElement = engine.FindElement(dstElementId.AgentId, dstElementId.ElementId);

            srcTable = srcDmsElement.GetTable(OutputsTable);
            dstTable = dstDmsElement.GetTable(InputsTable);
        }
    }
}
