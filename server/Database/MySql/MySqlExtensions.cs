// * Authorized for personal/commerical use
// * Open Source
// * Slayer [ Private Conquer Server ] - Revora

using System.Text.RegularExpressions;

namespace Revora
{
    public unsafe static class MySqlExtensions
    {
        public static string MySqlEscape(this string usString)
        {
            if (usString == null) return null;
            // SQL Encoding for MySQL Recommended here:
            // http://au.php.net/manual/en/function.mysql-real-escape-string.php
            // it escapes \r, \n, \x00, \x1a, baskslash, single quotes, and double quotes
            return usString.Replace("'", "''");
        }
    }
}