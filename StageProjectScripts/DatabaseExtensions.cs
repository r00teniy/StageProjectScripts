// by _gile from autodesk forums
using System.Collections;
using System.Collections.Generic;

namespace Autodesk.AutoCAD.DatabaseServices
{
    /// <summary>
    /// Provides extension methods for the Database type
    /// </summary>
    public static class DatabaseExtension
    {
        /// <summary>
        /// Gets the drawing custom properties.
        /// </summary>
        /// <param name="db">Database instance this method applies to.</param>
        /// <returns>A strongly typed dictionary containing the entries.</returns>
        public static Dictionary<string, string> GetCustomProperties(this Database db)
        {
            Dictionary<string, string> result = new();
            IDictionaryEnumerator dictEnum = db.SummaryInfo.CustomProperties;
            while (dictEnum.MoveNext())
            {
                DictionaryEntry entry = dictEnum.Entry;
                result.Add((string)entry.Key, (string)entry.Value);
            }
            return result;
        }

        /// <summary>
        /// Gets a drawing custom property.
        /// </summary>
        /// <param name="db">Database instance this method applies to.</param>
        /// <param name="key">Property key.</param>
        /// <returns>The property value or null if not found</returns>
        public static string GetCustomProperty(this Database db, string key)
        {
            DatabaseSummaryInfoBuilder sumInfo = new(db.SummaryInfo);
            IDictionary custProps = sumInfo.CustomPropertyTable;
            return (string)custProps[key];
        }

        /// <summary>
        /// Sets a property value
        /// </summary>
        /// <param name="db">Database instance this method applies to.</param>
        /// <param name="key">Property key.</param>
        /// <param name="value">Property value.</param>
        public static void SetCustomProperty(this Database db, string key, string value)
        {
            DatabaseSummaryInfoBuilder infoBuilder = new(db.SummaryInfo);
            IDictionary custProps = infoBuilder.CustomPropertyTable;
            if (custProps.Contains(key))
                custProps[key] = value;
            else
                custProps.Add(key, value);
            db.SummaryInfo = infoBuilder.ToDatabaseSummaryInfo();
        }
    }
}