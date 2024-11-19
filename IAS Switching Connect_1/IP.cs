namespace IAS_Switching_Connect_1
{
    using System;
    using System.Diagnostics;
    using System.Threading;
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

        private IDmsElement dstDmsElement;
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

        private enum Status
        {
            Disabled = 0,
            Enabled = 1,
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

            var srcDisabled = Convert.ToInt32(srcRow[2 /*Status*/]) == (int)Status.Disabled;
            var dstDisabled = Convert.ToInt32(dstRow[2 /*Status*/]) == (int)Status.Disabled;

            var srcIpAddress = Convert.ToString(srcRow[5 /*Single Destination IP*/]);
            var srcPort = Convert.ToInt32(srcRow[6 /*Single Destination Port*/]);
            var srcIp = Convert.ToString(srcRow[27 /*Single Source IP*/]);

            dstTable.GetColumn<string>(1546).SetValue(destinationId, KeyType.PrimaryKey, srcIpAddress);
            Thread.Sleep(1000);
            dstTable.GetColumn<int?>(1547).SetValue(destinationId, KeyType.PrimaryKey, srcPort);
            Thread.Sleep(1000);
            dstTable.GetColumn<string>(1577).SetValue(destinationId, KeyType.PrimaryKey, srcIp);

            if (!Retry(ValidateSets, new TimeSpan(0, 1, 0), srcIpAddress, srcPort))
            {
                ErrorMessageDialog.ShowMessage(engine, $"IP Address, Port and/or Source IP were not set on Destination.");
                return;
            }

            if (srcDisabled)
            {
                srcTable.GetColumn<int?>(1643).SetValue(sourceId, KeyType.PrimaryKey, (int)Status.Enabled);
            }

            if (dstDisabled)
            {
                dstTable.GetColumn<int?>(1543).SetValue(destinationId, KeyType.PrimaryKey, (int)Status.Enabled);
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
            dstDmsElement = dms.GetElement(dstElementId);

            srcTable = srcDmsElement.GetTable(OutputsTable);
            dstTable = dstDmsElement.GetTable(InputsTable);
        }

        private bool ValidateSets(string ipAddress, int port)
        {
            var row = dstDmsElement.GetTable(InputsTable).GetRow(destinationId);
            return Convert.ToString(row[5]) == ipAddress && Convert.ToInt32(row[6]) == port;
        }

        private bool Retry(Func<string, int, bool> func, TimeSpan timeout, string listenerIP, int listenerPort)
        {
            bool success;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            do
            {
                success = func(listenerIP, listenerPort);
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
