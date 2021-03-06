﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using Microsoft.VisualStudio.DebuggerVisualizers;
using System.Data.OleDb;
using System.Reflection;
using LinqToExcel.Extensions.Reflection;
using System.Data;
using LinqToExcel.Extensions.Object;
using System.IO;

namespace LinqToExcel
{
    /// <summary>
    /// Queries the Excel worksheet using an OLEDB connection
    /// </summary>
    public class ExcelOLEDB
    {
        /// <summary>
        /// Executes the query based upon the Linq statement against the Excel worksheet
        /// </summary>
        /// <param name="expression">Expression created from the Linq statement</param>
        /// <param name="fileName">File path to the Excel workbook</param>
        /// <param name="columnMapping">
        /// Property to column mapping. 
        /// Properties are the dictionary keys and the dictionary values are the corresponding column names.
        /// </param>
        /// <param name="worksheetName">Name of the Excel worksheet</param>
        /// <returns>Returns the results from the query</returns>
        public object ExecuteQuery(Expression expression, Type dataType, string fileName, ExcelVersion fileType, Dictionary<string, string> columnMapping, string worksheetName)
        {
            //Build the SQL string
            SQLExpressionVisitor sql = new SQLExpressionVisitor();
            sql.BuildSQLStatement(expression, columnMapping, worksheetName, fileName, fileType);

            PropertyInfo[] props = sql.SheetType.GetProperties();

            //string connString = (fileType == ExcelVersion.Csv) ?
            //    string.Format(@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=""text;HDR=Yes;FMT=Delimited;""",
            //        Path.GetDirectoryName(fileName)) :
            //    string.Format(@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=""Excel 8.0;HDR=YES;""", fileName);
            string connString = null;
            switch (fileType)
            {
                case ExcelVersion.Csv:
                    connString = string.Format(@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=""text;HDR=Yes;FMT=Delimited;""", Path.GetDirectoryName(fileName));
                    break;
                case ExcelVersion.PreExcel2007:
                    connString = string.Format(@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=""Excel 8.0;HDR=YES;""", fileName);
                    break;
                case ExcelVersion.Excel2007:
                    connString = string.Format(@"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={0};Extended Properties=""Excel 12.0 Xml;HDR=YES""", fileName);
                    break;
            }
            
            object results = Activator.CreateInstance(typeof(List<>).MakeGenericType(sql.SheetType));
            using (OleDbConnection conn = new OleDbConnection(connString))
            using (OleDbCommand command = conn.CreateCommand())
            {                
                conn.Open();
                command.CommandText = sql.SQLStatement;
                command.Parameters.Clear();
                command.Parameters.AddRange(sql.Parameters.ToArray());
                OleDbDataReader data = command.ExecuteReader();
                
                //Get the excel column names
                List<string> columns = new List<string>();
                DataTable sheetSchema = data.GetSchemaTable();
                foreach (DataRow row in sheetSchema.Rows)
                    columns.Add(row["ColumnName"].ToString());

                if (sql.SheetType == typeof(Row))
                {
                    Dictionary<string, int> columnIndexMapping = new Dictionary<string, int>();
                    for (int i = 0; i < columns.Count; i++)
                        columnIndexMapping[columns[i]] = i;
                    
                    while (data.Read())
                    {
                        IList<Cell> cells = new List<Cell>();
                        for (int i = 0; i < columns.Count; i++)
                            cells.Add(new Cell(data[i]));
                        results.CallMethod("Add", new Row(cells, columnIndexMapping));
                    }
                }
                else
                {
                    while (data.Read())
                    {
                        object result = Activator.CreateInstance(sql.SheetType);
                        foreach (PropertyInfo prop in props)
                        {
                            //Set the column name to the property mapping if there is one, else use the property name for the column name
                            string columnName = (columnMapping.ContainsKey(prop.Name)) ? columnMapping[prop.Name] : prop.Name;
                            if (columns.Contains(columnName))
                                result.SetProperty(prop.Name, Convert.ChangeType(data[columnName], prop.PropertyType));
                        }
                        results.CallMethod("Add", result);
                    }
                }
            }
            return results;
        }
    }
}
