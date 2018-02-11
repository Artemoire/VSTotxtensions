using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text.Editor;
using StringLiteralOffsetAdornment.Helpers;
using StringLiteralOffsetAdornment.Logic;
using StringLiteralOffsetAdornment.Utils;

namespace StringLiteralOffsetAdornment
{
    /// <summary>
    /// Adornment class that draws a square box in the top right hand corner of the viewport
    /// </summary>
    internal sealed class StringLiteralOffsetAdornment
    {
        /// <summary>
        /// Offset of the caret inside a string literal.
        /// Value is -1 when the caret is not positioned inside a string literal
        /// </summary>
        private int literalOffset;

        /// <summary>
        /// Text block adornment
        /// </summary>
        private readonly TextBlock adornment;

        /// <summary>
        /// Text view to add the adornment on.
        /// </summary>
        private readonly IWpfTextView view;

        /// <summary>
        /// The layer for the adornment.
        /// </summary>
        private readonly IAdornmentLayer adornmentLayer;

        /// <summary>
        /// The logic used to calculate offset of
        /// </summary>
        private SyntaxNode _root;
        public SyntaxNode Root { get { return _root ?? (_root = Document?.GetSyntaxRootAsync().Result); } set { _root = value; } }        

        /// <summary>
        /// The document used to call for syntax root after changes
        /// </summary>
        private readonly Lazier<Document> _document;      
        public Document Document { get { return _document.Value; } }

        /// <summary>
        /// Initializes a new instance of the <see cref="StringLiteralOffsetAdornment"/> class.
        /// Creates a square image and attaches an event handler to the layout changed event that
        /// adds the the square in the upper right-hand corner of the TextView via the adornment layer
        /// </summary>
        /// <param name="view">The <see cref="IWpfTextView"/> upon which the adornment will be drawn</param>
        public StringLiteralOffsetAdornment(IWpfTextView view)
        {
            this.view = view ?? throw new ArgumentNullException("view");
            _document = new Lazier<Document>(CodeAnalysisDocumentHelpers.TryGetDocument);

            literalOffset = -1;

            adornment = new TextBlock
            {
                Background = Brushes.BlueViolet,
                Foreground = Brushes.White,
                Padding = new Thickness(3, 3, 3, 3),
                TextAlignment = TextAlignment.Center
            };

            adornmentLayer = view.GetAdornmentLayer("StringLiteralOffsetAdornment");

            this.view.Caret.PositionChanged += Caret_PositionChanged;
            this.view.LayoutChanged += this.OnSizeChanged;
            this.view.TextBuffer.PostChanged += TextBuffer_PostChanged;
        }


        private void TextBuffer_PostChanged(object sender, EventArgs e)
        {
            _root = Document?.GetSyntaxRootAsync().Result;
            TextChanged(view.Caret.Position.BufferPosition.Position);
        }

        private void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            TextChanged(e.NewPosition.BufferPosition.Position);
        }

        private void TextChanged(int caretAbsoluteOffset)
        {
            var offset = StringLiteralOffsetLogic.CalcStringLiteralPosition(Root, caretAbsoluteOffset);
            if (offset != literalOffset)
                adornment.Text = offset.ToString();
            literalOffset = offset;
            RepaintBox();
        }

        /// <summary>
        /// Event handler for viewport layout changed event. Adds adornment at the top right corner of the viewport.
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Event arguments</param>
        private void OnSizeChanged(object sender, EventArgs e)
        {
            RepaintBox();
        }

        private void RepaintBox()
        {
            // Clear the adornment layer of previous adornments
            adornmentLayer.RemoveAllAdornments();

            if (literalOffset > -1)
            {
                var caretPos = view.Caret.Position.BufferPosition;
                var charBounds = view
                    .GetTextViewLineContainingBufferPosition(caretPos)
                    .GetCharacterBounds(caretPos);

                var top = charBounds.Top - adornment.ActualHeight;
                var left = charBounds.Left - view.ViewportLeft;
                if (view.ViewportTop > top)
                    top = charBounds.Bottom;

                Canvas.SetLeft(adornment, left);
                Canvas.SetTop(adornment, top);


                adornmentLayer.AddAdornment(AdornmentPositioningBehavior.ViewportRelative, null, null, adornment, null);
            }
        }
    }
}
