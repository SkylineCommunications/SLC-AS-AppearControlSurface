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

namespace IAS_Switching_Disconnect_1
{
	using System;
	using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

    /// <summary>
    /// Represents a DataMiner Automation script.
    /// </summary>
	public class Script
	{
        private const int SRTOutputsTable = 12000;
        private const int SRTInputsTable = 14000;
        private const int IPOutputsTable = 1600;
        private const int IPInputsTable = 1500;

        private string sourceId;
        private string destinationId;
        private string sourceElement;
        private string destinationElement;

        private IDms dms;
        private IDmsElement srcDmsElement;
        private IDmsElement dstDmsElement;

        private enum Status
        {
            Disabled = 0,
            Enabled = 1,
        }

        /// <summary>
        /// The script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
        public void Run(IEngine engine)
		{
            // DO NOT REMOVE THIS COMMENTED-OUT CODE OR THE SCRIPT WON'T RUN!
            // DataMiner evaluates if the script needs to launch in interactive mode.
            // This is determined by a simple string search looking for "engine.ShowUI" in the source code.
            // However, because of the toolkit NuGet package, this string cannot be found here.
            // So this comment is here as a workaround.
            //// engine.ShowUI();

            try
            {
				RunSafe(engine);
			}
			catch (ScriptAbortException)
			{
				// Catch normal abort exceptions (engine.ExitFail or engine.ExitSuccess)
				throw; // Comment if it should be treated as a normal exit of the script.
			}
			catch (ScriptForceAbortException)
			{
				// Catch forced abort exceptions, caused via external maintenance messages.
				throw;
			}
			catch (ScriptTimeoutException)
			{
				// Catch timeout exceptions for when a script has been running for too long.
				throw;
			}
			catch (InteractiveUserDetachedException)
			{
				// Catch a user detaching from the interactive script by closing the window.
				// Only applicable for interactive scripts, can be removed for non-interactive scripts.
				throw;
			}
			catch (Exception e)
			{
				engine.ExitFail("Run|Something went wrong: " + e);
			}
		}

        private void RunSafe(IEngine engine)
        {
            dms = engine.GetDms();
            var type = GetOneDeserializedValue(engine.GetScriptParam("Type").Value);
            sourceId = GetOneDeserializedValue(engine.GetScriptParam("Source ID").Value);
            sourceElement = GetOneDeserializedValue(engine.GetScriptParam("Source Element").Value);
            destinationId = GetOneDeserializedValue(engine.GetScriptParam("Destination ID").Value);
            destinationElement = GetOneDeserializedValue(engine.GetScriptParam("Destination Element").Value);

            if (String.IsNullOrWhiteSpace(type))
            {
                return;
            }

            InitializeGlobalVariables(engine);

            if (type == "SRT")
            {
                StartSRTDisconnect(engine);
            }
            else
            {
                StartIPDisconnect(engine);
            }
        }

        private string GetOneDeserializedValue(string scriptParam)
        {
            if (scriptParam.Contains("[") && scriptParam.Contains("]"))
            {
                return JsonConvert.DeserializeObject<List<string>>(scriptParam)[0];
            }
            else
            {
                return scriptParam;
            }
        }

        private void InitializeGlobalVariables(IEngine engine)
        {
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
        }

        private void StartIPDisconnect(IEngine engine)
        {
            var srcTable = srcDmsElement.GetTable(IPOutputsTable);
            var dstTable = dstDmsElement.GetTable(IPInputsTable);

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

            dstTable.GetColumn<int?>(1543).SetValue(destinationId, KeyType.PrimaryKey, (int)Status.Disabled);
            Thread.Sleep(1000);
            dstTable.GetColumn<string>(1546).SetValue(destinationId, KeyType.PrimaryKey, "-2" /*NA*/);
            Thread.Sleep(1000);
            dstTable.GetColumn<string>(1547).SetValue(destinationId, KeyType.PrimaryKey, "-2" /*NA*/);

            if (!Retry(ValidateStatus, new TimeSpan(0, 1, 0), false, false))
            {
                ErrorMessageDialog.ShowMessage(engine, $"IP Destination side not disabled.");
            }
        }

        private void StartSRTDisconnect(IEngine engine)
        {
            var srcTable = srcDmsElement.GetTable(SRTOutputsTable);
            var dstTable = dstDmsElement.GetTable(SRTInputsTable);

            if (!ValidateKeysExists(engine, srcTable, dstTable))
            {
                return;
            }

            var srcRow = srcTable.GetRow(sourceId);
            var dstRow = dstTable.GetRow(destinationId);

            if (!ValidateRowExist(engine, srcRow, dstRow))
            {
                return;
            }

            var callerOnSource = Convert.ToString(srcRow[6] /*SRT Mode*/) == "CALLER";
            if (callerOnSource)
            {
                srcTable.GetColumn<int?>(12054).SetValue(sourceId, KeyType.PrimaryKey, (int)Status.Disabled);
            }
            else
            {
                dstTable.GetColumn<int?>(14054).SetValue(destinationId, KeyType.PrimaryKey, (int)Status.Disabled);
            }

            if (!Retry(ValidateStatus, new TimeSpan(0, 1, 0), callerOnSource, true))
            {
                ErrorMessageDialog.ShowMessage(engine, $"SRT Caller side not disabled.");
            }
        }

        private bool ValidateKeysExists(IEngine engine, IDmsTable srcTable, IDmsTable dstTable)
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

        private bool ValidateRowExist(IEngine engine, object[] srcRow, object[] dstRow)
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

        private bool ValidateStatus(bool callerOnSource, bool isSrt)
        {
            if (isSrt)
            {
                if (callerOnSource)
                {
                    var row = srcDmsElement.GetTable(SRTOutputsTable).GetRow(sourceId);
                    return Convert.ToInt32(row[3]) == (int)Status.Disabled;
                }
                else
                {
                    var row = dstDmsElement.GetTable(SRTInputsTable).GetRow(destinationId);
                    return Convert.ToInt32(row[3]) == (int)Status.Disabled;
                }
            }
            else
            {
                var row = dstDmsElement.GetTable(IPInputsTable).GetRow(destinationId);
                return Convert.ToInt32(row[2]) == (int)Status.Disabled;
            }
        }

        private bool Retry(Func<bool, bool, bool> func, TimeSpan timeout, bool callerOnSource, bool isSrt)
        {
            bool success;

            Stopwatch sw = new Stopwatch();
            sw.Start();

            do
            {
                success = func(callerOnSource, isSrt);
                if (!success)
                {
                    Thread.Sleep(3000);
                }
            }
            while (!success && sw.Elapsed <= timeout);

            return success;
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