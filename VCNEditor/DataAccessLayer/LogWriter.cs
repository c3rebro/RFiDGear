/*
 * Created by SharpDevelop.
 * Date: 31.08.2017
 * Time: 20:32
 *
 */

using System;
using System.IO;

namespace VCNEditor.DataAccessLayer
{
    /// <summary>
    /// Description of LogWriter.
    /// </summary>
    public static class LogWriter
    {
        private static StreamWriter textStream;

        private static readonly string _logFileName = "err.log";

        /// <summary>
        ///
        /// </summary>
        /// <param name="entry"></param>
        public static void CreateLogEntry(string entry)
        {
            string _logFilePath = Path.Combine(new DirectoryInfo(new System.Uri(typeof(VCNEditor.ViewModel.VCNEditorViewModel).Assembly.CodeBase).LocalPath).Parent.FullName, "log");

            if (!Directory.Exists(_logFilePath))
            {
                Directory.CreateDirectory(_logFilePath);
            }

            try
            {
                if (!File.Exists(Path.Combine(_logFilePath, _logFileName)))
                {
                    textStream = File.CreateText(Path.Combine(_logFilePath, _logFileName));
                }
                else
                {
                    textStream = File.AppendText(Path.Combine(_logFilePath, _logFileName));
                }

                textStream.WriteAsync(string.Format("{0}" + Environment.NewLine, entry.Replace("\r\n", "; ")));
                textStream.Close();
                textStream.Dispose();
            }
            catch
            {
            }
        }
    }
}