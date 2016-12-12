﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using NLog;
using pdfforge.PDFCreator.Core.Printing.Printer;
using pdfforge.PDFCreator.Core.SettingsManagement;
using pdfforge.PDFCreator.Utilities;

namespace pdfforge.PDFCreator.Core.Printing.Printing
{
    /// <summary>
    ///     The PrintFileHelperBase class provides reusable functionality for printing files
    /// </summary>
    public abstract class PrintFileHelperBase : IPrintFileHelper
    {
        private readonly IFileAssoc _fileAssoc;
        private readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private PrintCommandGroup _printCommands = new PrintCommandGroup();
        private readonly IPrinterHelper _printerHelper;
        private readonly ISettingsProvider _settingsProvider;

        protected PrintFileHelperBase(IPrinterHelper printerHelper, ISettingsProvider settingsProvider, IFileAssoc fileAssoc)
        {
            _printerHelper = printerHelper;
            _settingsProvider = settingsProvider;
            _fileAssoc = fileAssoc;
        }

        public string PdfCreatorPrinter { get; set; }

        /// <summary>
        ///     Add a single file. The file is checked and dialogs are presented to the user, if there are problems.
        /// </summary>
        /// <param name="file">
        ///     A single file. If this is the path of a directory or an unprintable file, an error message will be
        ///     shown.
        /// </param>
        /// <returns>true, if all files are printable</returns>
        public bool AddFile(string file)
        {
            return AddFiles(new[] {file});
        }

        /// <summary>
        ///     Add multiple files. The files are checked - if there are problems, abstract functions (that might present dialogs
        ///     to the user) get called.
        /// </summary>
        /// <param name="files">
        ///     A list of files. If this contains a directory or files are not printable, an error message will be
        ///     shown.
        /// </param>
        /// <returns>true, if all files are printable</returns>
        public bool AddFiles(IEnumerable<string> files)
        {
            var printerName = GetPrinterName();

            foreach (var f in files)
            {
                _printCommands.Add(new PrintCommand(f, printerName, _fileAssoc));
            }

            var directories = _printCommands.FindAll(p => Directory.Exists(p.Filename));

            if (directories.Count > 0)
            {
                DirectoriesNotSupportedHint();
                return false;
            }

            if (!_printCommands.IsPrintable)
            {
                var sb = new StringBuilder("The following file(s) could not be converted:");
                var unprintable = _printCommands.UnprintableCommands;
                foreach (var file in unprintable)
                {
                    sb.AppendLine(file.Filename);
                }
                _logger.Error(sb.ToString);
                if (!UnprintableFilesQuery(unprintable))
                    return false;

                _printCommands.RemoveUnprintableCommands();
            }

            return true;
        }

        /// <summary>
        ///     Prints all files in the list.
        /// </summary>
        /// <returns>true, if all files could be printed</returns>
        public bool PrintAll()
        {
            if (string.IsNullOrEmpty(PdfCreatorPrinter))
            {
                _logger.Error("No PDFCreator is installed.");
                return false;
            }

            var requiresDefaultPrinter = _printCommands.RequiresDefaultPrinter;
            var defaultPrinter = _printerHelper.GetDefaultPrinter();

            try
            {
                if (requiresDefaultPrinter)
                {
                    if (_settingsProvider.Settings.ApplicationSettings.AskSwitchDefaultPrinter)
                    {
                        if (!QuerySwitchDefaultPrinter())
                            return false;
                    }

                    _printerHelper.SetDefaultPrinter(PdfCreatorPrinter);
                }

                return _printCommands.PrintAll();
            }
            finally
            {
                if (requiresDefaultPrinter)
                    _printerHelper.SetDefaultPrinter(defaultPrinter);
                _printCommands = new PrintCommandGroup();
            }
        }

        private string GetPrinterName()
        {
            if (string.IsNullOrWhiteSpace(PdfCreatorPrinter))
                PdfCreatorPrinter = _settingsProvider.Settings.ApplicationSettings.PrimaryPrinter;

            return _printerHelper.GetApplicablePDFCreatorPrinter(PdfCreatorPrinter);
        }

        protected abstract void DirectoriesNotSupportedHint();
        protected abstract bool UnprintableFilesQuery(IList<PrintCommand> unprintable);
        protected abstract bool QuerySwitchDefaultPrinter();
    }
}