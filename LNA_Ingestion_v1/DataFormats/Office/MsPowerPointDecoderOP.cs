// Copyright (c) Microsoft. All rights reserved.

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.KernelMemory.DataFormats.Office
{
    public class MsPowerPointDecoderOP
    {
        private readonly string _slideNumberTemplate;
        private readonly string _endOfSlideMarkerTemplate;

        /// <param name="slideNumberTemplate">Template used for the optional slide number added at the start of each slide</param>
        /// <param name="endOfSlideMarkerTemplate">Template used for the optional text added at the end of each slide</param>
        public MsPowerPointDecoderOP(
            string slideNumberTemplate = "# Slide {number}",
            string endOfSlideMarkerTemplate = "# End of slide {number}")
        {
            this._slideNumberTemplate = slideNumberTemplate;
            this._endOfSlideMarkerTemplate = endOfSlideMarkerTemplate;
        }

        /// <summary>
        /// Return the text contained by the powerpoint presentation
        /// </summary>
        /// <param name="filename">File name</param>
        /// <param name="withSlideNumber">Whether to include the slide number before the text</param>
        /// <param name="withEndOfSlideMarker">Whether to add a marker after the text of each slide</param>
        /// <param name="skipHiddenSlides">Whether to skip hidden slides</param>
        /// <returns>The text extracted from the presentation</returns>
        public string DocToText(
            string filename,
            bool withSlideNumber = true,
            bool withEndOfSlideMarker = false,
            bool skipHiddenSlides = true)
        {
            using var stream = File.OpenRead(filename);
            return this.DocToText(stream, skipHiddenSlides: skipHiddenSlides, withEndOfSlideMarker: withEndOfSlideMarker, withSlideNumber: withSlideNumber);
        }

        /// <summary>
        /// Return the text contained by the powerpoint presentation
        /// </summary>
        /// <param name="data">File content in binary form</param>
        /// <param name="withSlideNumber">Whether to include the slide number before the text</param>
        /// <param name="withEndOfSlideMarker">Whether to add a marker after the text of each slide</param>
        /// <param name="skipHiddenSlides">Whether to skip hidden slides</param>
        /// <returns>The text extracted from the presentation</returns>
        public string DocToText(
            BinaryData data,
            bool withSlideNumber = true,
            bool withEndOfSlideMarker = false,
            bool skipHiddenSlides = true)
        {
            using var stream = data.ToStream();
            return this.DocToText(stream, skipHiddenSlides: skipHiddenSlides, withEndOfSlideMarker: withEndOfSlideMarker, withSlideNumber: withSlideNumber);
        }

        /// <summary>
        /// Return the text contained by the powerpoint presentation
        /// </summary>
        /// <param name="data">File content in stream form</param>
        /// <param name="withSlideNumber">Whether to include the slide number before the text</param>
        /// <param name="withEndOfSlideMarker">Whether to add a marker after the text of each slide</param>
        /// <param name="skipHiddenSlides">Whether to skip hidden slides</param>
        /// <returns>The text extracted from the presentation</returns>
        public string DocToText(
            Stream data,
            bool withSlideNumber = true,
            bool withEndOfSlideMarker = false,
            bool skipHiddenSlides = true)
        {
            using PresentationDocument presentationDocument = PresentationDocument.Open(data, false);
            var sb = new StringBuilder();

            if (presentationDocument.PresentationPart is PresentationPart presentationPart
                && presentationPart.Presentation is Presentation presentation
                && presentation.SlideIdList is SlideIdList slideIdList
                && slideIdList.Elements<SlideId>().ToList() is List<SlideId> slideIds and { Count: > 0 })
            {
                var slideNumber = 0;
                foreach (SlideId slideId in slideIds)
                {
                    slideNumber++;
#pragma warning disable CA1508 // code taken from official MS docs
                    if ((string?)slideId.RelationshipId is string relationshipId
                        && presentationPart.GetPartById(relationshipId) is SlidePart slidePart
                        && slidePart != null
                        && slidePart.Slide?.Descendants<DocumentFormat.OpenXml.Drawing.Text>().ToList() is List<DocumentFormat.OpenXml.Drawing.Text> texts and { Count: > 0 })
#pragma warning restore CA1508
                    {
                        // Check if the slide is hidden and whether to skip it
                        // PowerPoint does not set the value of this property, in general, unless the slide is to be hidden
                        // The only way the Show property would exist and have a value of true would be if the slide had been hidden and then unhidden
                        // - Show is null: default, slide is visible
                        // - Show is false: the slide is hidden
                        // - Show is true: the slide is visible
                        bool isVisible = slidePart.Slide.Show ?? true;
                        if (skipHiddenSlides && !isVisible) { continue; }

                        var slideContent = new StringBuilder();
                        for (var i = 0; i < texts.Count; i++)
                        {
                            var text = texts[i];
                            slideContent.Append(text.Text);
                            if (i < texts.Count - 1)
                            {
                                slideContent.Append(' ');
                            }
                        }

                        // Skip the slide if there is no text
                        if (slideContent.Length < 1) { continue; }

                        // Prepend slide number before the slide text
                        if (withSlideNumber)
                        {
                            sb.AppendLine(this._slideNumberTemplate.Replace("{number}", $"{slideNumber}", StringComparison.OrdinalIgnoreCase));
                        }

                        sb.Append(slideContent);
                        sb.AppendLine();

                        // Append the end of slide marker
                        if (withEndOfSlideMarker)
                        {
                            sb.AppendLine(this._endOfSlideMarkerTemplate.Replace("{number}", $"{slideNumber}", StringComparison.OrdinalIgnoreCase));
                        }
                    }
                }
            }

            return sb.ToString().Trim();
        }
    }
}
