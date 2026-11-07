using System;
using System.IO;
using System.Runtime.CompilerServices;

using RFiDGear.ExtensionLibraries.CardCheck;

namespace RFiDGear.Extensions.CardCheckAssistant.Services
{
    /// <summary>
    /// Finds out whether the support library of the extension can be used, before a page depends on it. MEF cannot see
    /// a missing sibling file: the extension is found and created without it, and the failure would come later, when
    /// something first touches a library type. So the first touch is made here, on purpose, inside a try block, and a
    /// library that is missing, of the wrong bitness or of another version gives a page that says so instead of an
    /// unhandled exception in the host. The check cannot see a file that disappears later: once the library is loaded
    /// in the process it stays loaded, so a removed file shows up at the next start of the application.
    /// </summary>
    internal static class ComponentCheck
    {
        /// <summary>The file name of the library, as shown to the operator.</summary>
        internal const string LibraryFileName = "RFiDGear.ExtensionLibraries.CardCheck.dll";

        /// <summary>Runs the first touch of the library and says whether it worked.</summary>
        /// <param name="touchLibrary">Uses a type of the library; <see cref="TouchLibrary"/> in the extension.</param>
        /// <returns><c>false</c> if the library file is missing, cannot be loaded or does not match this extension.</returns>
        internal static bool IsAvailable(Func<object?> touchLibrary)
        {
            try
            {
                touchLibrary();

                return true;
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is FileLoadException || ex is BadImageFormatException
                || ex is TypeLoadException || ex is MissingMemberException)
            {
                return false;
            }
        }

        /// <summary>
        /// Uses a type of the library. It is a method of its own and never inlined, so the library is needed only when
        /// this method is compiled, which happens inside the try block of <see cref="IsAvailable"/>.
        /// </summary>
        /// <returns>The library's log object; it only proves that the library could be used.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static object? TouchLibrary() => CardCheckServices.CreateLog();
    }
}
