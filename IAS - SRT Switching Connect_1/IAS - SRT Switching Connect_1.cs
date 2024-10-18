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
	using System.Linq;
	using Shared.Dialogs;
	using Shared.SharedMethods;
	using Skyline.DataMiner.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Automation;
	using Skyline.DataMiner.Core.DataMinerSystem.Common;

    /// <summary>
    /// Represents a DataMiner Automation script.
    /// </summary>
	public class Script
	{
        private IEngine engine;
        private IDmsElement srcDmsElement;
        private IDmsElement dstDmsElement;
        private Element srcElement;
        private Element dstElement;
        private IDmsTable srcTable;
        private IDmsTable dstTable;

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
            this.engine = engine;

            var sourceId = SharedMethods.GetOneDeserializedValue(engine.GetScriptParam("SRT Source").Value);
            var destinationId = SharedMethods.GetOneDeserializedValue(engine.GetScriptParam("SRT Destination").Value);

            InitializeGlobalVariables(engine);

            if (!ValidateKeysExists(sourceId, destinationId, srcTable, dstTable))
            {
                return;
            }

            var srcRow = srcTable.GetRow(sourceId);
            var dstRow = dstTable.GetRow(destinationId);

            if (!ValidateRowExist(sourceId, destinationId, srcRow, dstRow))
            {
                return;
            }

            var srcSrtMode = Convert.ToString(srcRow[6] /*SRT Mode*/);
            var dstSrtMode = Convert.ToString(dstRow[7]/*SRT Mode*/);

            if (srcSrtMode == dstSrtMode)
            {
                var message = $"Selected Source and Destination have the same Mode ({Convert.ToString(srcRow[6] /*SRT Mode*/)}). Please select one Caller and one Listener.";
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
            var srcDisabled = srcStatus == (int)SharedMethods.Status.Disabled;
            var dstDisabled = dstStatus == (int)SharedMethods.Status.Disabled;

            if (srcDisabled)
            {
                srcElement.SetParameterByPrimaryKey(12054, sourceId, (int)SharedMethods.Status.Enabled);
            }

            if (dstDisabled)
            {
                dstElement.SetParameterByPrimaryKey(14054, destinationId, (int)SharedMethods.Status.Enabled);
            }
        }

        private void InitializeGlobalVariables(IEngine engine)
        {
            var dms = engine.GetDms();

            var sourceElement = SharedMethods.GetOneDeserializedValue(engine.GetScriptParam("Source Element").Value);
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

            srcDmsElement = dms.GetElement(srcElementId);
            dstDmsElement = dms.GetElement(dstElementId);
            srcElement = engine.FindElement(srcElementId.AgentId, srcElementId.ElementId);
            dstElement = engine.FindElement(dstElementId.AgentId, dstElementId.ElementId);
            srcTable = srcDmsElement.GetTable(12000 /*SRT Outputs*/);
            dstTable = dstDmsElement.GetTable(14000 /*SRT Inputs*/);
        }

        private bool ValidateKeysExists(string sourceId, string destinationId, IDmsTable srcTable, IDmsTable dstTable)
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

        private bool ValidateRowExist(string sourceId, string destinationId, object[] srcRow, object[] dstRow)
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

            var ipInterfacesTableData = dmsElement.GetTable(2000 /*IP Interfaces*/).GetData();
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