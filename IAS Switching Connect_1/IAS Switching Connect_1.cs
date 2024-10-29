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

namespace IAS_Switching_Connect_1
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Utils.InteractiveAutomationScript;

    /// <summary>
    /// Represents a DataMiner Automation script.
    /// </summary>
	public class Script
    {
        /// <summary>
        /// The script entry point.
        /// </summary>
        /// <param name="engine">Link with SLAutomation process.</param>
		public static void Run(IEngine engine)
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

		private static void RunSafe(IEngine engine)
		{
			var type = GetOneDeserializedValue(engine.GetScriptParam("Type").Value);
			var sourceId = GetOneDeserializedValue(engine.GetScriptParam("Source ID").Value);
			var sourceElement = GetOneDeserializedValue(engine.GetScriptParam("Source Element").Value);
			var destinationId = GetOneDeserializedValue(engine.GetScriptParam("Destination ID").Value);
			var destinationElement = GetOneDeserializedValue(engine.GetScriptParam("Destination Element").Value);

			if (String.IsNullOrWhiteSpace(type))
			{
				return;
			}

			if (type == "SRT")
			{
				var srt = new SRT(engine, sourceId, destinationId, sourceElement, destinationElement);
				srt.StartConnection();
			}
			else
			{
                var ip = new IP(engine, sourceId, destinationId, sourceElement, destinationElement);
                ip.StartConnection();
            }
		}

		private static string GetOneDeserializedValue(string scriptParam)
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