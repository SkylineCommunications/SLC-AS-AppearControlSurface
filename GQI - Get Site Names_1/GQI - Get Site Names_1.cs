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

namespace GQI_GetSiteNames
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Emit;
    using Skyline.DataMiner.Analytics.GenericInterface;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Messages;
    using Skyline.DataMiner.Net.Messages.SLDataGateway;

    [GQIMetaData(Name = "GQI - Get Site Names")]
    public sealed class GetSiteNames : IGQIDataSource, IGQIOnInit, IGQIInputArguments
    {
        private readonly GQIStringArgument rootViewNameArg = new GQIStringArgument("Root View Name") { IsRequired = true };

        private string _rootViewName;
        private GQIDMS _dms;

        public OnInitOutputArgs OnInit(OnInitInputArgs args)
        {
            _dms = args.DMS;
            return new OnInitOutputArgs();
        }

        public GQIColumn[] GetColumns()
        {
            return new GQIColumn[]
            {
                new GQIStringColumn("ID"),
                new GQIStringColumn("Name"),
            };
        }

        public GQIArgument[] GetInputArguments()
        {
            return new GQIArgument[] { rootViewNameArg };
        }

        public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
        {
            args.TryGetArgumentValue(rootViewNameArg, out _rootViewName);
            return new OnArgumentsProcessedOutputArgs();
        }

        public GQIPage GetNextPage(GetNextPageInputArgs args)
        {
            var rows = new List<GQIRow>();
            try
            {
                return GetSiteNameRows(rows);
            }
            catch (Exception ex)
            {
                rows.Add(CreateDebugRow($"{ex.Message}"));
                return new GQIPage(rows.ToArray())
                {
                    HasNextPage = false,
                };
            }
        }

        private GQIPage GetSiteNameRows(List<GQIRow> rows)
        {
            var viewRequest = new GetInfoMessage(InfoType.ViewInfo);
            var viewResponse = GetDataByType<ViewInfoEventMessage>(viewRequest);

            if (viewResponse == null)
            {
                return null;
            }

            var mainView = viewResponse.Find(x => x.Name.Equals(_rootViewName));
            var childViews = mainView.DirectChildViews;

            foreach (var childView in childViews)
            {
                var siteView = viewResponse.Find(x => x.ID.Equals(childView));

                var siteName = siteView.Name;

                var cells = new[]
                {
                     new GQICell { Value = Convert.ToString(siteView.ID) },
                     new GQICell { Value = siteName },
                };

                var row = new GQIRow(cells);
                rows.Add(row);
            }

            return new GQIPage(rows.ToArray())
            {
                HasNextPage = false,
            };
        }

        private List<T> GetDataByType<T>(DMSMessage request)
        {
            try
            {
                return _dms.SendMessages(request).OfType<T>().ToList();
            }
            catch (Exception)
            {
                return new List<T>();
            }
        }

        private GQIRow CreateDebugRow(string message)
        {
            var cells = new[]
            {
                     new GQICell { Value = message },
                     new GQICell {},
            };

            var row = new GQIRow(cells);

            return row;
        }
    }
}