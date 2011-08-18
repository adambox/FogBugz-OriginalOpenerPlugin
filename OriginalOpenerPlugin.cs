/* Copyright 2011 Fog Creek Software, Inc. */

using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

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
    public class OriginalOpenerPlugin : Plugin, IPluginFilterJoin, IPluginFilterCommit,
                 IPluginFilterOptions, IPluginGridColumn, IPluginDatabase, IPluginSearchAxis
    {
        protected const string PLUGIN_ID = "OriginalOpenerPlugin@fogcreek.com";
        protected const string PLUGIN_TABLE_NAME = "FilterIxPersonOpenedBy";
        protected const string PERSON_TABLE_ALIAS = "PersonOrigOpener";
        protected const string BUGEVENT_TABLE_ALIAS = "BugOpenedEvent";
        protected const string FILTER_HEADING = "Original Opener";
        protected const string SEARCH_AXIS = "originallyopenedby";
        protected const string FILTER_STRING = "originally opened by";
        protected const string PLUGIN_FIELD_NAME = "ixPersonOriginallyOpenedBy";
        protected const string PLUGIN_PRIMARY_KEY_NAME = "ixFilterIxPersonOpenedBy";
        protected const int PLUGIN_DB_SCHEMA_VERSION = 1;
        protected const int IXPERSON_INVALID = -2;
        protected const int IXPERSON_ANY = -1;
        protected const int IXPERSON_MIN = 2;
        protected const int IXPERSON_FOGBUGZ_USER = -1;
        protected const string FILTER_OPTION_ANY = "Anybody";
        protected const string SVERB_INCOMING_EMAIL = "Incoming Email";
        protected const string SVERB_OPENED = "Opened";

        protected string sPluginTableNamePrefixed;
        protected string sPluginFieldNamePrefixed;
        protected int ixPersonOriginallyOpenedByPreCommit = IXPERSON_ANY;

        public OriginalOpenerPlugin(CPluginApi api) : base(api)
        {
            sPluginTableNamePrefixed = api.Database.PluginTableName(PLUGIN_TABLE_NAME);
            sPluginFieldNamePrefixed = api.AddPluginPrefix(PLUGIN_FIELD_NAME);
        }

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
            if (api.Request[sPluginFieldNamePrefixed] != null)
            {
                ixPersonOriginallyOpenedByPreCommit =
                Convert.ToInt32(Filter.GetPluginField(PLUGIN_ID, PLUGIN_FIELD_NAME));

                int ixPersonOriginallyOpenedBy = IXPERSON_INVALID;

                /* use tryparse in case the URL querystring value isn't a valid integer */
                if (Int32.TryParse(api.Request[sPluginFieldNamePrefixed].ToString(),
                                   out ixPersonOriginallyOpenedBy))
                {
                    // if the requested value isn't an actual CPerson, set the plugin field to "any"
                    if (!IsValidPerson(ixPersonOriginallyOpenedBy))
                        ixPersonOriginallyOpenedBy = IXPERSON_ANY;
                    Filter.SetPluginField(PLUGIN_ID, PLUGIN_FIELD_NAME,
                        ixPersonOriginallyOpenedBy);
                }
            }

            return true;

        }

        public void FilterCommitRollback(CFilter Filter)
        {
            Filter.SetPluginField(PLUGIN_ID, PLUGIN_FIELD_NAME, ixPersonOriginallyOpenedByPreCommit);
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
            filterOption.sQueryParam = sPluginFieldNamePrefixed;
            filterOption.fShowDialogItem = true;
            filterOption.sHeader = FILTER_HEADING;
            filterOption.sName = PLUGIN_FIELD_NAME;
            filterOption.SetDefault(IXPERSON_ANY.ToString(), FILTER_OPTION_ANY);
            return new CFilterOption[] { filterOption };
        }

        public CSelectQuery FilterOptionsQuery(CFilter filter)
        {
            int ixPersonOriginallyOpenedBy = GetFieldValueFromFilter(filter);

            CSelectQuery query = api.Database.NewSelectQuery("Bug");
            if (ixPersonOriginallyOpenedBy < IXPERSON_MIN)
                return query;

            query.AddLeftJoin("BugEvent", GetBugOpenedEventLeftJoinClause(), BUGEVENT_TABLE_ALIAS);
            query.AddWhere(string.Format("{0}.ixPerson = @ixPersonOriginallyOpenedBy",
                                         BUGEVENT_TABLE_ALIAS));
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

            if (ixPersonOriginallyOpenedBy >= IXPERSON_MIN)
            {
                sFilterString = string.Format("{0} {1}",
                                              FILTER_STRING,
                                              api.Person.GetPerson(ixPersonOriginallyOpenedBy).sFullName);

                /* the second parameter to CFilterStringList.Add specifies the
                 * CFilterOption by name to display when the text is clicked */
                list.Add(sFilterString, PLUGIN_FIELD_NAME);
            }
            return list;
        }

        public string FilterOptionsUrlParams(CFilter filter)
        {
            /* To make the filter saveable, we need to assign a querystring
             * parameter to this filter setting. */

            int ixPersonOriginallyOpenedBy = GetFieldValueFromFilter(filter);

            if (ixPersonOriginallyOpenedBy < IXPERSON_MIN)
                return "";
            else
                return string.Format("{0}={1}",
                                     sPluginFieldNamePrefixed,
                                     ixPersonOriginallyOpenedBy);
        }

        public bool FilterBugEntryCanCreate(CFilter filter)
        {
            // you cannot set an opener on a new case, so don't allow quick-adding if the filter
            // includes an original opener
            return (GetFieldValueFromFilter(filter) < IXPERSON_MIN);
        }

        public string FilterBugEntryUrlParams(CFilter filter)
        {
            return "";
        }

        #endregion


        #region IPluginDatabase Members

        public CTable[] DatabaseSchema()
        {
            CTable table = api.Database.NewTable(sPluginTableNamePrefixed);
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

        #region IPluginGridColumn Members

        // original opener comes up as blank if the case originated as an email
        public string[] GridColumnDisplay(CGridColumn col, CBug[] rgBug, bool fPlainText)
        {
            string[] rgsValues = new string[rgBug.Length];

            for (int i = 0; i < rgBug.Length; i++)
            {
                CBug bug = rgBug[i];
                bug.IgnorePermissions = true;

                object oQueryField = bug.QueryField(PLUGIN_FIELD_NAME);
                int ixPersonOriginallyOpenedBy = (oQueryField == DBNull.Value) ?
                                                 IXPERSON_INVALID :
                                                 Convert.ToInt32(oQueryField);
                if (ixPersonOriginallyOpenedBy == IXPERSON_INVALID)
                {
                    rgsValues[i] = "";
                }
                else
                {
                    CPerson personOrigOpener = api.Person.GetPerson(ixPersonOriginallyOpenedBy);
                    rgsValues[i] = (api.Person.GetCurrentPerson().CanSee(personOrigOpener.ixPerson)) ?
                                   GetPersonLink(personOrigOpener) :
                                   personOrigOpener.sFullName;
                }
            }
            return rgsValues;
        }

        public CBugQuery GridColumnQuery(CGridColumn col)
        {
            CBugQuery bugQuery = api.Bug.NewBugQuery();

            bugQuery.AddSelect(string.Format("{0}.ixPerson as {1}, {2}.sFullName",
                                             BUGEVENT_TABLE_ALIAS,
                                             PLUGIN_FIELD_NAME,
                                             PERSON_TABLE_ALIAS));
            
            bugQuery.AddLeftJoin("BugEvent",
                                 GetBugOpenedEventLeftJoinClause(),
                                 BUGEVENT_TABLE_ALIAS);
            bugQuery.AddLeftJoin("Person",
                                 string.Format("{0}.ixPerson = {1}.ixPerson",
                                               PERSON_TABLE_ALIAS,
                                               BUGEVENT_TABLE_ALIAS),
                                 PERSON_TABLE_ALIAS);
            return bugQuery;
        }

        public CBugQuery GridColumnSortQuery(CGridColumn col, bool fDescending, bool fIncludeSelect)
        {
            CBugQuery bq = api.Bug.NewBugQuery();

            if (fIncludeSelect)
                bq.AddSelect(string.Format("{0}.sFullName",
                                           PERSON_TABLE_ALIAS));

            if (col.iType == 0)
            {
                bq.AddOrderBy(string.Format("{0}.sFullName {1}",
                                            PERSON_TABLE_ALIAS,
                                            fDescending ? "DESC" : "ASC"));
            }

            return bq;
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

        #region IPluginSearchAxis Members

        public bool SearchAxisIsFullText(string sAxis, SearchType nType, string sValue)
        {
            return false;
        }

        public CSelectQuery SearchAxisQuery(string sAxis, SearchType nType, string sValue)
        {
            if (nType == SearchType.Bug && sAxis.ToLower() == SEARCH_AXIS)
            {
                CSelectQuery query = api.Database.NewSelectQuery("Bug");
                query.AddSelect("Bug.ixBug");
                query.AddLeftJoin("BugEvent", GetBugOpenedEventLeftJoinClause(), BUGEVENT_TABLE_ALIAS);

                int ixOriginalOpenerSearched = IXPERSON_INVALID;
                string sOriginalOpenerSearched = sValue;

                if (sOriginalOpenerSearched == "*")
                    return query;

                // are they looking for a specific user by ix with "User X" or "X"?
                Regex regex = new Regex("^(user )?(\\-?[0-9]+)$", RegexOptions.IgnoreCase);
                if (regex.IsMatch(sOriginalOpenerSearched))
                {
                    ixOriginalOpenerSearched =
                        Int32.Parse(regex.Match(sOriginalOpenerSearched).Groups[2].Value);
                    if (ixOriginalOpenerSearched < IXPERSON_MIN)
                        api.Notifications.AddError(string.Format("Invalid ixPerson for axis {0} value: '{1}'",
                                                                 FILTER_HEADING,
                                                                 ixOriginalOpenerSearched));
                    query.AddWhere(string.Format("{0}.ixPerson = @ixPersonOriginallyOpenedBy",
                                                 BUGEVENT_TABLE_ALIAS));
                    query.SetParamInt("@ixPersonOriginallyOpenedBy", ixOriginalOpenerSearched);
                }
                else
                {
                    //CPersonQuery pq = api.Person.NewPersonQuery();
                    CSelectQuery pq = api.Database.NewSelectQuery("Person");
                    pq.AddSelect("ixPerson");
                    pq.AddSubstringLike("Person.sFullName", sOriginalOpenerSearched);
                    pq.IgnorePermissions = true;

                    query.AddWhereIn(string.Format("{0}.ixPerson", BUGEVENT_TABLE_ALIAS), pq);
                }
                return query;
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Read the filter's ixPersonOriginallyOpenedBy. If it's invalid, fix it (set it to "any")
        /// </summary>
        /// <param name="filter">Filter to check (and possibly update)</param>
        /// <returns>The validated ixPersonOriginallyOpenedBy</returns>
        protected int GetFieldValueFromFilter(CFilter filter)
        {
            filter.IgnorePermissions = true;
            int ixPersonFromFilter = Convert.ToInt32(filter.GetPluginField(PLUGIN_ID,
                                                                           PLUGIN_FIELD_NAME));
            if (!IsValidPerson(ixPersonFromFilter))
            {
                ixPersonFromFilter = IXPERSON_ANY;
                filter.SetPluginField(PLUGIN_ID, PLUGIN_FIELD_NAME, ixPersonFromFilter);
            }
            return ixPersonFromFilter;
        }

        protected string GetPersonLink(CPerson person)
        {
            return string.Format("<a href=\"default.asp?pg=pgPersonInfo&ixPerson={0}\">{1}</a>",
                                 person.ixPerson,
                                 person.sFullName);
        }

        protected bool IsValidPerson(int ixPerson)
        {
            return (api.Person.GetPerson(ixPerson) != null);
        }

        protected string GetBugOpenedEventLeftJoinClause()
        {
            // Cases opened by email have their first BugEvent's sVerb = 'Incoming Email', edited by
            // the primary contact (same as the ixPersonOpenedBy. Other events with that sVerb
            // are edited by ixPerson -1 (the "FogBugz" user)
            // this clause gets the first bugevent for emails, normal, community and anon user cases
            // note this matches FogBugz's current behavior for community user cases searchable
            // only as openedby:primary_contact_name
            return string.Format("{0}.ixBug = Bug.ixBug " +
                                 "AND (" +
                                       "({0}.sVerb = '{1}') OR " +
                                       "({0}.sVerb = '{2}' AND {0}.ixPerson <> {3})" +
                                     ")",
                                 BUGEVENT_TABLE_ALIAS,
                                 SVERB_OPENED,
                                 SVERB_INCOMING_EMAIL,
                                 IXPERSON_FOGBUGZ_USER);
        }

        #endregion
    }
}