/* Copyright 2011 Fog Creek Software, Inc. */

using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Data;

using FogCreek.FogBugz.Plugins;
using FogCreek.FogBugz.Plugins.Api;
using FogCreek.FogBugz.Plugins.Entity;
using FogCreek.FogBugz.Plugins.Interfaces;
using FogCreek.FogBugz;
using FogCreek.FogBugz.UI;
using FogCreek.FogBugz.UI.Dialog;
using FogCreek.FogBugz.Database;
using FogCreek.FogBugz.Database.Entity;
using FogCreek.FogBugz.Globalization;
using System.Collections;

namespace FogCreek.Plugins.OriginalOpenerPlugin
{
    public class OriginalOpenerPlugin : Plugin, IPluginFilterJoin,
        IPluginFilterDisplay, IPluginFilterCommit, IPluginFilterOptions, IPluginGridColumn, IPluginDatabase
    {
        protected const string PLUGIN_ID =
            "OriginalOpenerPlugin@fogcreek.com";
        protected const string PLUGIN_TABLE_NAME = "FilterIxPersonOpenedBy";
        protected const string FILTER_HEADING = "Original Opener";
        protected const string PLUGIN_FIELD_NAME = "ixPersonOriginallyOpenedBy";
        protected const string PLUGIN_PRIMARY_KEY_NAME = "ixFilterIxPersonOpenedBy";
        protected const int PLUGIN_DB_SCHEMA_VERSION = 1;
        protected string sPluginTableName;

        public OriginalOpenerPlugin(CPluginApi api) : base(api)
        {
            sPluginTableName = api.Database.PluginTableName(PLUGIN_TABLE_NAME);
        }

        #region IPluginFilterDisplay Members

        public CDialogItem[] FilterDisplayEdit(CFilter filter)
        {
            /* if you specify one or more CFilterOptions, they will appear
             * on the edit filter page automatically. You don't need to
             * explicitly add dialog items here. */
            return null;
        }

        public string[] FilterDisplayListFields(CFilter filter)
        {
            int ixPersonOriginallyOpenedBy = GetFieldValueFromFilter(filter);

            string s = "FilterDisplayListFields";

            return new string[] { s };
        }

        public string[] FilterDisplayListHeaders()
        {
            return new string[] { FILTER_HEADING };
        }

        #endregion

        #region IPluginFilterJoin Members

        public string[] FilterJoinTables()
        {
            return new string[] { PLUGIN_TABLE_NAME };
        }

        #endregion

        #region IPluginFilterCommit Members

        public void FilterCommitAfter(CFilter Filter)
        {
        }

        public bool FilterCommitBefore(CFilter Filter)
        {
            if (api.Request[api.AddPluginPrefix(PLUGIN_FIELD_NAME)] != null)
            {
                int ixPersonOriginallyOpenedBy = -1;

                /* use tryparse in case the URL querystring value isn't a valid integer */
                if (Int32.TryParse(api.Request[api.AddPluginPrefix(PLUGIN_FIELD_NAME)].ToString(),
                                   out ixPersonOriginallyOpenedBy) &&
                    (ixPersonOriginallyOpenedBy > -1)
                )
                    Filter.SetPluginField(PLUGIN_ID, PLUGIN_FIELD_NAME,
                        ixPersonOriginallyOpenedBy);
            }

            return true;

        }

        public void FilterCommitRollback(CFilter Filter)
        {
        }

        #endregion

        #region IPluginFilterOptions Members

        public CFilterOption[] FilterOptions(CFilter filter)
        {
            /* Create a new filter option to be included in the list of options
             * available in the "Filter:" drop-down at the top of the case
             * list. */

            int ixPersonOriginallyOpenedBy = GetFieldValueFromFilter(filter);

            CFilterOption filterOption = api.Filter.NewFilterOption();

            /* Specify a single person select drop-down */
            filterOption.SetSelectOnePerson(ixPersonOriginallyOpenedBy);
            filterOption.sQueryParam = api.AddPluginPrefix(PLUGIN_FIELD_NAME);
            filterOption.fShowDialogItem = true;
            filterOption.sHeader = FILTER_HEADING;
            filterOption.sName = PLUGIN_FIELD_NAME;
            return new CFilterOption[] { filterOption };
        }

        public CSelectQuery FilterOptionsQuery(CFilter filter)
        {
            /* Specify a query for the list of cases to be returned when the
             * filter is imposed. */

            int ixPersonOriginallyOpenedBy = GetFieldValueFromFilter(filter);

            CSelectQuery query = api.Database.NewSelectQuery("Bug");
            if (ixPersonOriginallyOpenedBy < 1)
                return query;

            query.AddLeftJoin("BugEvent", "BugOpenedEvent.ixBug = Bug.ixBug", "BugOpenedEvent");
            query.AddWhere("BugOpenedEvent.sVerb = 'Opened'"); // this isn't localized!
            query.AddWhere("BugOpenedEvent.ixPerson = @ixPersonOriginallyOpenedBy");
            query.SetParamInt("@ixPersonOriginallyOpenedBy", ixPersonOriginallyOpenedBy);

            return query;
        }

        public CFilterStringList FilterOptionsString(CFilter filter)
        {
            int ixPersonOriginallyOpenedBy = GetFieldValueFromFilter(filter);

            CFilterStringList list = new CFilterStringList();

            /* Return a string for the "Filter:" message at the top of the case list 
             * so that the the user can clearly interpret current filter settings. */

            string sFilterString;

            if (ixPersonOriginallyOpenedBy > 1)
            {
                sFilterString = string.Format("Originally Opened By {0}",
                                              api.Person.GetPerson(ixPersonOriginallyOpenedBy).sFullName);

                /* the second parameter to CFilterStringList.Add specifies the
                 * CFilterOption by name to display when the text is clicked */
                list.Add(sFilterString, "awesomeness");
            }
            return list;
        }

        public string FilterOptionsUrlParams(CFilter filter)
        {
            /* To make the filter saveable, we need to assign a querystring
             * parameter to this filter setting. */

            int ixPersonOriginallyOpenedBy = GetFieldValueFromFilter(filter);

            if (ixPersonOriginallyOpenedBy < 1)
                return "";
            else
                return string.Format("{0}={1}",
                                     api.AddPluginPrefix(PLUGIN_FIELD_NAME),
                                     GetFieldValueFromFilter(filter));
        }

        public bool FilterBugEntryCanCreate(CFilter filter)
        {
            // you cannot set an opener on a new case, so don't allow quick-adding if the filter
            // includes an original opener
            return (GetFieldValueFromFilter(filter) < 0);
        }

        public string FilterBugEntryUrlParams(CFilter filter)
        {
            return "";
        }

        #endregion


        #region IPluginDatabase Members

        public CTable[] DatabaseSchema()
        {
            CTable table = api.Database.NewTable(sPluginTableName);
            table.sDesc = "Adds a filter field for the case's original opener.";
            table.AddAutoIncrementPrimaryKey(PLUGIN_PRIMARY_KEY_NAME);
            table.AddIntColumn("ixFilter", true, 1);
            table.AddIntColumn("ixPerson", true, 1);
            table.AddIntColumn(PLUGIN_FIELD_NAME, true, 0);

            return new CTable[] { table };
        }

        public int DatabaseSchemaVersion()
        {
            return PLUGIN_DB_SCHEMA_VERSION;
        }

        public void DatabaseUpgradeAfter(int ixVersionFrom, int ixVersionTo, CDatabaseUpgradeApi apiUpgrade)
        {
        }

        public void DatabaseUpgradeBefore(int ixVersionFrom, int ixVersionTo, CDatabaseUpgradeApi apiUpgrade)
        {
        }

        #endregion

        protected int GetFieldValueFromFilter(CFilter filter)
        {
            return Convert.ToInt32(filter.GetPluginField(PLUGIN_ID, PLUGIN_FIELD_NAME));
        }

        #region IPluginGridColumn Members

        public string[] GridColumnDisplay(CGridColumn col, CBug[] rgBug, bool fPlainText)
        {
            string[] rgsValues = new string[rgBug.Length];

            for (int i = 0; i < rgBug.Length; i++)
            {
                CBug bug = rgBug[i];
                bug.IgnorePermissions = true; // need to make sure the user can see the orig opener. if not, show "user 123"
                CPerson personOrigOpener = api.Person.GetPerson(Convert.ToInt32(bug.QueryField("ixPersonOriginallyOpenedBy")));
                rgsValues[i] = personOrigOpener.sFullName;
            }
            return rgsValues;
        }

        public CBugQuery GridColumnQuery(CGridColumn col)
        {
            CBugQuery bugQuery = api.Bug.NewBugQuery();
            
            bugQuery.AddSelect("BugOpenedEvent.ixPerson as ixPersonOriginallyOpenedBy");
            bugQuery.AddLeftJoin("BugEvent", "BugOpenedEvent.ixBug = Bug.ixBug AND BugOpenedEvent.sVerb = 'Opened'", "BugOpenedEvent"); // this isn't localized!

            return bugQuery;
        }

        public CBugQuery GridColumnSortQuery(CGridColumn col, bool fDescending, bool fIncludeSelect)
        {
            return api.Bug.NewBugQuery();
        }

        public CGridColumn[] GridColumns()
        {
            CGridColumn gridColumn = api.Grid.CreateGridColumn();
            gridColumn.sName = FILTER_HEADING;
            gridColumn.sTitle = FILTER_HEADING;
            gridColumn.iType = 0;
            gridColumn.sGroup = "Person";

            return new CGridColumn[] { gridColumn };
        }

        #endregion
    }
}