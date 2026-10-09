using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenCodeProxyLauncher
{
    /// <summary>
    /// 极简 SQLite 只读封装。
    ///
    /// 直接 P/Invoke 系统自带的 winsqlite3.dll（Windows 10+ 内置，SQLite 3.x），
    /// 因此不需要引入任何 NuGet 包，也不影响单文件打包。
    /// </summary>
    internal static class Sqlite
    {
        private const string Library = "winsqlite3.dll";

        private const int Ok = 0;
        private const int Row = 100;
        private const int Done = 101;
        private const int OpenReadOnly = 0x00000001;
        private const int ColumnTypeNull = 5;

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_libversion();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int numBytes, out IntPtr stmt, IntPtr tail);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr stmt);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr stmt);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_count(IntPtr stmt);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_type(IntPtr stmt, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr stmt, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern long sqlite3_column_int64(IntPtr stmt, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern double sqlite3_column_double(IntPtr stmt, int index);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        /// <summary>winsqlite3.dll 是否可用（拿不到就说明系统不支持）。</summary>
        public static bool IsAvailable()
        {
            try
            {
                IntPtr version = sqlite3_libversion();
                return version != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 以只读方式执行一条查询。单元格按实际类型返回 long / double / string / null。
        /// </summary>
        public static List<object[]> Query(string databasePath, string sql)
        {
            var rows = new List<object[]>();

            IntPtr db = IntPtr.Zero;
            IntPtr stmt = IntPtr.Zero;

            try
            {
                byte[] path = Encoding.UTF8.GetBytes(databasePath);
                int rc = sqlite3_open_v2(path, out db, OpenReadOnly, IntPtr.Zero);
                if (rc != Ok)
                {
                    throw new InvalidOperationException("sqlite3_open_v2 rc=" + rc + " " + ErrorMessage(db));
                }

                byte[] query = Encoding.UTF8.GetBytes(sql);
                rc = sqlite3_prepare_v2(db, query, query.Length, out stmt, IntPtr.Zero);
                if (rc != Ok)
                {
                    throw new InvalidOperationException("sqlite3_prepare_v2 rc=" + rc + " " + ErrorMessage(db));
                }

                while (true)
                {
                    rc = sqlite3_step(stmt);
                    if (rc == Done)
                    {
                        break;
                    }

                    if (rc != Row)
                    {
                        throw new InvalidOperationException("sqlite3_step rc=" + rc + " " + ErrorMessage(db));
                    }

                    rows.Add(ReadRow(stmt));
                }
            }
            finally
            {
                if (stmt != IntPtr.Zero)
                {
                    sqlite3_finalize(stmt);
                }

                if (db != IntPtr.Zero)
                {
                    sqlite3_close(db);
                }
            }

            return rows;
        }

        private static object[] ReadRow(IntPtr stmt)
        {
            int count = sqlite3_column_count(stmt);
            var values = new object[count];

            for (int index = 0; index < count; index++)
            {
                int type = sqlite3_column_type(stmt, index);

                if (type == ColumnTypeNull)
                {
                    values[index] = null;
                }
                else if (type == 1)
                {
                    values[index] = sqlite3_column_int64(stmt, index);
                }
                else if (type == 2)
                {
                    values[index] = sqlite3_column_double(stmt, index);
                }
                else
                {
                    IntPtr text = sqlite3_column_text(stmt, index);
                    values[index] = text == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(text);
                }
            }

            return values;
        }

        private static string ErrorMessage(IntPtr db)
        {
            try
            {
                if (db == IntPtr.Zero)
                {
                    return "(no db handle)";
                }

                IntPtr message = sqlite3_errmsg(db);
                return message == IntPtr.Zero ? "(unknown)" : Marshal.PtrToStringUTF8(message);
            }
            catch
            {
                return "(unknown)";
            }
        }
    }
}
