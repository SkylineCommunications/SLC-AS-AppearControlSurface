namespace Shared.Dialogs
{
    using System;
    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Utils.InteractiveAutomationScript;

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
