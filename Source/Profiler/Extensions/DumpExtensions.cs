using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyProfiler.Profiler.Core;
using Verse;

namespace HarmonyProfiler.Profiler.Extensions
{
    public static class DumpExtensions
    {
        private const int DoubleDigits = 5;

        private static readonly string[] Headers = new[]
        {
            "Assembly",
            "Method",
            "AverageTime",
            "Ticks",
            "MinTime",
            "MaxTime",
            "AvgTime*Ticks in ms",
            "AvgKB",
            "AllocKB",
            "Patches"
        };

        public static string DumpToCsv(this List<StopwatchRecord> records)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var header in Headers)
            {
                sb.Append($"{header};");
            }

            sb.AppendLine();

            foreach (var rr in records)
            {
                sb.AppendLine(rr.ToCsvRow(DoubleDigits));
            }
            return sb.ToString();
        }

        public static string DumpToSlk(this List<StopwatchRecord> records)
        {
            if (records.Count == 0)
                return String.Empty;

            var cells = records.ConvertToStringArray();
            if (cells[0].Length != Headers.Length)
            {
                Log.Error($"[DumpToSlk] ReportRecords columns != {Headers.Length}");
                return String.Empty;
            }
            
            StringBuilder sb = new StringBuilder();
            // push slk id
            sb.AppendLine("ID;PMP");
            // push columns width
            for (int column = 0; column < Headers.Length; column++)
            {
                int columnWidth = Math.Max(Headers[column].Length, cells.Max(x => x[column].Length)) + 1;
                int columnNum = column + 1;
                sb.AppendLine($"F;W{columnNum} {columnNum} {columnWidth}");
            }
            // push header
            for (int column = 0; column < Headers.Length; column++)
            {
                sb.AppendLine($"C;Y1;X{column + 1};K\"{Headers[column]}\"");
            }

            // push rows
            for (int row = 0; row < cells.Length; row++)
            {
                if (cells[row].Length != Headers.Length)
                {
                    Log.Error($"[DumpToSlk] ReportRecords columns != {Headers.Length}. Row: {row}");
                    return String.Empty;
                }

                int rowNum = row + 2; // + excel + header row
                sb.AppendLine($"C;Y{rowNum};X1;K\"{cells[row][0]}\"");
                sb.AppendLine($"C;Y{rowNum};X2;K\"{cells[row][1]}\"");
                sb.AppendLine($"C;Y{rowNum};X3;K{cells[row][2]}");
                sb.AppendLine($"C;Y{rowNum};X4;K{cells[row][3]}");
                sb.AppendLine($"C;Y{rowNum};X5;K{cells[row][4]}");
                sb.AppendLine($"C;Y{rowNum};X6;K{cells[row][5]}");
                sb.AppendLine($"C;Y{rowNum};X7;K{cells[row][6]}");
                sb.AppendLine($"C;Y{rowNum};X8;K{cells[row][7]}");
                sb.AppendLine($"C;Y{rowNum};X9;K{cells[row][8]}");
                sb.AppendLine($"C;Y{rowNum};X10;K\"{cells[row][9]}\"");
            }

            sb.AppendLine("E");

            return sb.ToString();
        }
        
        public static string[][] ConvertToStringArray(this List<StopwatchRecord> records)
        {
            return records.Select(x => x.ToStringArray(DoubleDigits)).ToArray();
        }
    }
}