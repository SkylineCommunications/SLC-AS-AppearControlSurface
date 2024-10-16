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

namespace IAS_SRT_Switching_Connect_1
{
	using System;
	using Library.Dialogs;
	using Library.SharedMethods;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;

    /// <summary>
    /// Represents a DataMiner Automation script.
    /// </summary>
	public class Script
	{
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
			var dms = engine.GetDms();

			var sourceId = SharedMethods.GetOneDeserializedValue(engine.GetScriptParam("SRT Source").Value);
			var sourceElement = SharedMethods.GetOneDeserializedValue(engine.GetScriptParam("Source Element").Value);
			var destinationId = SharedMethods.GetOneDeserializedValue(engine.GetScriptParam("SRT Destination").Value);
			var destinationElement = SharedMethods.GetOneDeserializedValue(engine.GetScriptParam("Destination Element").Value);

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
			var srcElement = engine.FindElement(srcElementId.AgentId, srcElementId.ElementId);
			var dstElement = engine.FindElement(dstElementId.AgentId, dstElementId.ElementId);

			var srcTable = srcDmsElement.GetTable(12000 /*SRT Outputs*/);
			var dstTable = dstDmsElement.GetTable(14000 /*SRT Inputs*/);

			var srcRow = srcTable.GetRow(sourceId);
			var dstRow = dstTable.GetRow(destinationId);

			if (srcRow == null)
            {
                ErrorMessageDialog.ShowMessage(engine, $"Row not found on SRT Outputs table. Row key: {sourceId}");
                return;
            }
            else if (dstRow == null)
            {
                ErrorMessageDialog.ShowMessage(engine, $"Row not found on SRT Inputs table. Row key: {destinationId}");
                return;
            }
            else
            {
                // no action
            }

			if (Convert.ToString(srcRow[6] /*SRT Mode*/) == Convert.ToString(dstRow[7]/*SRT Mode*/))
			{
				var message = $"Selected Source and Destination have the same Mode ({Convert.ToString(srcRow[6] /*SRT Mode*/)}). Please select one Caller and one Listener.";
				ErrorMessageDialog.ShowMessage(engine, message);
				return;
            }

			var srcDisabled = Convert.ToInt32(srcRow[3 /*Status*/]) == (int)Status.Disabled;
			var dstDisabled = Convert.ToInt32(dstRow[3 /*Status*/]) == (int)Status.Disabled;

			// TODO: Set the right values for IP and Port
			var dstIpAddress = Convert.ToString(dstRow[3 /*Status*/]);
			var dstPort = Convert.ToString(dstRow[3 /*Status*/]);

			srcElement.SetParameterByPrimaryKey(14059, sourceId, dstIpAddress);
			srcElement.SetParameterByPrimaryKey(14060, sourceId, dstPort);

			if (srcDisabled)
			{
                srcElement.SetParameterByPrimaryKey(12054, sourceId, (int)Status.Enabled);
            }

			if (dstDisabled)
			{
                dstElement.SetParameterByPrimaryKey(14054, destinationId, (int)Status.Enabled);
            }
        }
    }
}