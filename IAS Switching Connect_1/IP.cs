namespace IAS_Switching_Connect_1
{
    using System;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Core.DataMinerSystem.Automation;
    using Skyline.DataMiner.Core.DataMinerSystem.Common;
    using Skyline.DataMiner.Utils.InteractiveAutomationScript;

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
            var srcPort = Convert.ToString(srcRow[6 /*Single Destination Port*/]);

            dstElement.SetParameterByPrimaryKey(1546, destinationId, srcIpAddress);
            dstElement.SetParameterByPrimaryKey(1547, destinationId, srcPort);

            if (srcDisabled)
            {
                srcElement.SetParameterByPrimaryKey(1643, sourceId, (int)Status.Enabled);
            }

            if (dstDisabled)
            {
                dstElement.SetParameterByPrimaryKey(1543, destinationId, (int)Status.Enabled);
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

    public class ErrorMessageDialog : Dialog
    {
        public ErrorMessageDialog(IEngine engine, string message) : base(engine)
        {
            // Set title
            Title = "Error";

            // Init widgets
            Label = new Label(message);

            // Define layout
            AddWidget(Label, 0, 0);
            AddWidget(OkButton, 1, 0);
        }

        public Button OkButton { get; } = new Button("OK") { Width = 100 };

        private Label Label { get; set; }

        public static void ShowMessage(IEngine engine, string message)
        {
            try
            {
                var dialog = new ErrorMessageDialog(engine, message);

                dialog.OkButton.Pressed += (sender, args) => engine.ExitSuccess("Close");

                var controller = new InteractiveController(engine);
                controller.ShowDialog(dialog);
            }
            catch (ScriptAbortException)
            {
                // ignore abort
            }
            catch (Exception e)
            {
                engine.ExitFail("Something went wrong: " + e);
            }
        }
    }
}
