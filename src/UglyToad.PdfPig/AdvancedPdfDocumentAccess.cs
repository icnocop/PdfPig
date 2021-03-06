﻿namespace UglyToad.PdfPig
{
    using System;
    using System.Collections.Generic;
    using Content;
    using Filters;
    using Parser.Parts;
    using Tokenization.Scanner;
    using Tokens;

    /// <inheritdoc />
    /// <summary>
    /// Provides access to rare or advanced features from the PDF specification.
    /// </summary>
    public class AdvancedPdfDocumentAccess : IDisposable
    {
        private readonly IPdfTokenScanner pdfScanner;
        private readonly IFilterProvider filterProvider;
        private readonly Catalog catalog;

        private bool isDisposed;

        internal AdvancedPdfDocumentAccess(IPdfTokenScanner pdfScanner,
            IFilterProvider filterProvider,
            Catalog catalog)
        {
            this.pdfScanner = pdfScanner ?? throw new ArgumentNullException(nameof(pdfScanner));
            this.filterProvider = filterProvider ?? throw new ArgumentNullException(nameof(filterProvider));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>
        /// Get any embedded files contained in this PDF document.
        /// Since PDF 1.3 any external file referenced by the document may have its contents embedded within the referring PDF file, 
        /// allowing its contents to be stored or transmitted along with the PDF file.
        /// </summary>
        /// <param name="embeddedFiles">The set of embedded files in this document.</param>
        /// <returns><see langword="true"/> if this document contains more than zero embedded files, otherwise <see langword="false"/>.</returns>
        public bool TryGetEmbeddedFiles(out IReadOnlyList<EmbeddedFile> embeddedFiles)
        {
            GuardDisposed();

            embeddedFiles = null;

            if (!catalog.CatalogDictionary.TryGet(NameToken.Names, pdfScanner, out DictionaryToken namesDictionary)
                || !namesDictionary.TryGet(NameToken.EmbeddedFiles, pdfScanner, out DictionaryToken embeddedFileNamesDictionary))
            {
                return false;
            }

            var embeddedFileNames = NameTreeParser.FlattenNameTreeToDictionary(embeddedFileNamesDictionary, pdfScanner, x => x);

            if (embeddedFileNames.Count == 0)
            {
                return false;
            }

            var result = new List<EmbeddedFile>();

            foreach (var keyValuePair in embeddedFileNames)
            {
                if (!DirectObjectFinder.TryGet(keyValuePair.Value, pdfScanner, out DictionaryToken fileDescriptorDictionaryToken)
                    || !fileDescriptorDictionaryToken.TryGet(NameToken.Ef, pdfScanner, out DictionaryToken efDictionary)
                    || !efDictionary.TryGet(NameToken.F, pdfScanner, out StreamToken fileStreamToken))
                {
                    continue;
                }

                var fileSpecification = string.Empty;
                if (fileDescriptorDictionaryToken.TryGet(NameToken.F, pdfScanner, out IDataToken<string> fileSpecificationToken))
                {
                    fileSpecification = fileSpecificationToken.Data;
                }

                var fileBytes = fileStreamToken.Decode(filterProvider);

                result.Add(new EmbeddedFile(keyValuePair.Key, fileSpecification, fileBytes, fileStreamToken));
            }

            embeddedFiles = result;

            return embeddedFiles.Count > 0;
        }

        private void GuardDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(AdvancedPdfDocumentAccess));
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            pdfScanner?.Dispose();
            isDisposed = true;
        }
    }
}