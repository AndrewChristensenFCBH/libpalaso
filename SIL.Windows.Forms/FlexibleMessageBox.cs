﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using L10NSharp;
namespace SIL.Windows.Forms
{
	/*  FlexibleMessageBox – A flexible replacement for the .NET MessageBox
	 *
	 *  Author:		Jörg Reichert (public@jreichert.de)
	 *  Contributors:   Thanks to: David Hall, Roink
	 *  Version:		1.3
	 *  Published at:   http://www.codeproject.com/Articles/601900/FlexibleMessageBox
	 *
	 ************************************************************************************************************
	 * Features:
	 *  - It can be simply used instead of MessageBox since all important static "Show"-Functions are supported
	 *  - It is small, only one source file, which could be added easily to each solution
	 *  - It can be resized and the content is correctly word-wrapped
	 *  - It tries to auto-size the width to show the longest text row
	 *  - It never exceeds the current desktop working area
	 *  - It displays a vertical scrollbar when needed
	 *  - It does support hyperlinks in text
	 *  - It allows copying of messages to clipboard
	 *
	 *  Because the interface is identical to MessageBox, you can add this single source file to your project
	 *  and use the FlexibleMessageBox almost everywhere you use a standard MessageBox.
	 *  The goal was NOT to produce as many features as possible but to provide a simple replacement to fit my
	 *  own needs. Feel free to add additional features on your own, but please left my credits in this class.
	 *
	 ************************************************************************************************************
	 * Usage examples:
	 *
	 *  FlexibleMessageBox.Show("Just a text");
	 *
	 *  FlexibleMessageBox.Show("A text", "A caption");
	 *
	 *  FlexibleMessageBox.Show("Some text with a link: www.google.com",
	 *							"Some caption",
	 *							MessageBoxButtons.AbortRetryIgnore,
	 *							MessageBoxIcon.Information,
	 *							MessageBoxDefaultButton.Button2);
	 *
	 *  var dialogResult = FlexibleMessageBox.Show("Do you know the answer to life the universe and everything?",
	 *											   "One short question",
	 *											   MessageBoxButtons.YesNo);
	 *
	 ************************************************************************************************************
	 *  THE SOFTWARE IS PROVIDED BY THE AUTHOR "AS IS", WITHOUT WARRANTY
	 *  OF ANY KIND, EXPRESS OR IMPLIED. IN NO EVENT SHALL THE AUTHOR BE
	 *  LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY ARISING FROM,
	 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OF THIS
	 *  SOFTWARE.
	 *
	 ************************************************************************************************************
	 * History:
	 *	Adapted for use in libpalaso
	 *  - Added override to default handling of LinkClicked event
	 *  - Made localizatin extenisible without altering code
	 *	Version 1.3 - 19.Dezember 2014
	 *  - Added refactoring function GetButtonText()
	 *  - Used CurrentUICulture instead of InstalledUICulture
	 *  - Added more button localizations. Supported languages are now: ENGLISH, GERMAN, SPANISH, ITALIAN
	 *  - Added standard MessageBox handling for "copy to clipboard" with <Ctrl> + <C> and <Ctrl> + <Insert>
	 *  - Tab handling is now corrected (only tabbing over the visible buttons)
	 *  - Added standard MessageBox handling for ALT-Keyboard shortcuts
	 *  - SetDialogSizes: Refactored completely: Corrected sizing and added caption driven sizing
	 *
	 *	Version 1.2 - 10.August 2013
	 *   - Do not ShowInTaskbar anymore (original MessageBox is also hidden in taskbar)
	 *   - Added handling for Escape-Button
	 *   - Adapted top right close button (red X) to behave like MessageBox (but hidden instead of deactivated)
	 *
	 *	Version 1.1 - 14.June 2013
	 *   - Some Refactoring
	 *   - Added internal form class
	 *   - Added missing code comments, etc.
	 *
	 *	Version 1.0 - 15.April 2013
	 *   - Initial Version
	 */
	public class FlexibleMessageBox
	{
		#region Public statics
		private static double _maxWidthFactor = 0.7;
		private static double _maxHeightFactor = 0.9;
		/// <summary>
		/// Defines the maximum width for all FlexibleMessageBox instances in percent of the working area.
		///
		/// Allowed values are 0.2 - 1.0 where:
		/// 0.2 means:  The FlexibleMessageBox can be at most 20% of the width of the working area.
		/// 1.0 means:  The FlexibleMessageBox can be as wide as the working area.
		///
		/// Default is: 0.7 (70% of the working area width)
		/// </summary>
		public static double MaxWidthFactor
		{
			get { return _maxWidthFactor; }
			set { _maxWidthFactor = Math.Max(Math.Min(value, 1.0), 0.2); }
		}
		/// <summary>
		/// Defines the maximum height for all FlexibleMessageBox instances in percent of the working area.
		///
		/// Allowed values are 0.2 - 1.0 where:
		/// 0.2 means:  The FlexibleMessageBox can be at most half as high as the working area.
		/// 1.0 means:  The FlexibleMessageBox can be as high as the working area.
		///
		/// Default is: 0.9 (90% of the working area height)
		/// </summary>
		public static double MaxHeightFactor
		{
			get { return _maxHeightFactor; }
			set { _maxHeightFactor = Math.Max(Math.Min(value, 1.0), 0.2); }
		}

		/// <summary>
		/// Defines the font for all FlexibleMessageBox instances.
		///
		/// Default is: SystemFonts.MessageBoxFont
		/// </summary>
		public static Font Font = SystemFonts.MessageBoxFont;

		/// <summary>
		/// Callers that use a localization strategy other than L10NSharp can use this property to obtain a full
		/// list of all localization keys and the default (English) value. These keys will be passed to the
		/// GetButtonText function if it is overridden.
		/// </summary>
		public static IEnumerable<KeyValuePair<string, string>> GetButtonTextLocalizationKeys
		{
			get
			{
				// Localization ID Prefixes here must match the hard-coded text in FlexibleMessageBoxForm.GetButtonText:
				yield return new KeyValuePair<string, string>("FlexibleMessageBoxBtn.Ok", "&OK");
				yield return new KeyValuePair<string, string>("FlexibleMessageBoxBtn.Cancel", "&Cancel");
				yield return new KeyValuePair<string, string>("FlexibleMessageBoxBtn.Yes", "&Yes");
				yield return new KeyValuePair<string, string>("FlexibleMessageBoxBtn.No", "&No");
				yield return new KeyValuePair<string, string>("FlexibleMessageBoxBtn.Abort", "&Abort");
				yield return new KeyValuePair<string, string>("FlexibleMessageBoxBtn.Retry", "&Retry");
				yield return new KeyValuePair<string, string>("FlexibleMessageBoxBtn.Ignore", "&Ignore");
			}
		}

		/// <summary>
		/// Callers that use a localization strategy other than L10NSharp can set this function to get a
		/// callback allowing them to supply a non-English button name.
		/// </summary>
		public static Func<string, string> GetButtonText { get; set; }

		/// <summary>
		/// Pass this in as the linkClickedAction to the Show methods to get a simple link-handler that fires off a
		/// process using the link text as it's parameter.
		/// </summary>
		public static LinkClickedEventHandler BasicLinkClickedEventHandler { get { return richTextBoxMessage_LinkClicked; } }
		/// <summary>
		/// Provides a basic implementation for handling the LinkClicked event of the richTextBoxMessage control.
		/// </summary>
		/// <param name="sender">The source of the event.</param>
		/// <param name="e">The <see cref="System.Windows.Forms.LinkClickedEventArgs"/> instance containing the event data.</param>
		private static void richTextBoxMessage_LinkClicked(object sender, LinkClickedEventArgs e)
		{
			try
			{
				Cursor.Current = Cursors.WaitCursor;
				Process.Start(e.LinkText);
			}
			catch (Exception)
			{
				//Let the caller of FlexibleMessageBoxForm decide what to do with this exception...
				throw;
			}
			finally
			{
				Cursor.Current = Cursors.Default;
			}
		}
		#endregion
		#region Public show functions
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(string text, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(null, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="owner">The owner.</param>
		/// <param name="text">The text.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(IWin32Window owner, string text, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="caption">The caption.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(string text, string caption, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="owner">The owner.</param>
		/// <param name="text">The text.</param>
		/// <param name="caption">The caption.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(IWin32Window owner, string text, string caption, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="caption">The caption.</param>
		/// <param name="buttons">The buttons.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="owner">The owner.</param>
		/// <param name="text">The text.</param>
		/// <param name="caption">The caption.</param>
		/// <param name="buttons">The buttons.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="caption">The caption.</param>
		/// <param name="buttons">The buttons.</param>
		/// <param name="icon">The icon.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns></returns>
		public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="owner">The owner.</param>
		/// <param name="text">The text.</param>
		/// <param name="caption">The caption.</param>
		/// <param name="buttons">The buttons.</param>
		/// <param name="icon">The icon.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
			MessageBoxIcon icon, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="caption">The caption.</param>
		/// <param name="buttons">The buttons.</param>
		/// <param name="icon">The icon.</param>
		/// <param name="defaultButton">The default button.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon,
			MessageBoxDefaultButton defaultButton, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(null, text, caption, buttons, icon, defaultButton, linkClickedAction);
		}
		/// <summary>
		/// Shows the specified message box.
		/// </summary>
		/// <param name="owner">The owner.</param>
		/// <param name="text">The text.</param>
		/// <param name="caption">The caption.</param>
		/// <param name="buttons">The buttons.</param>
		/// <param name="icon">The icon.</param>
		/// <param name="defaultButton">The default button.</param>
		/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
		/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
		/// highlighted in the message.</param>
		/// <returns>The dialog result.</returns>
		/// <returns>The dialog result.</returns>
		public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
			MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, LinkClickedEventHandler linkClickedAction = null)
		{
			return FlexibleMessageBoxForm.Show(owner, text, caption, buttons, icon, defaultButton, linkClickedAction);
		}
		#endregion
		#region Internal form class
		/// <summary>
		/// The form to show the customized message box.
		/// It is defined as an internal class to keep the public interface of the FlexibleMessageBox clean.
		/// As the comments below indicate, this was (presumably) originally created in Designer. We're leaving
		/// all the Designer-related stuff (including) comments, in case some day someone decides it is
		/// expedient to try to move it back out and make it designable.
		/// </summary>
		class FlexibleMessageBoxForm : Form
		{
			#region Form-Designer generated code
			/// <summary>
			/// Required Designer variable.
			/// </summary>
			private System.ComponentModel.IContainer components = null;
			/// <summary>
			/// Clean up any resources being used.
			/// </summary>
			/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
			protected override void Dispose(bool disposing)
			{
				if (disposing && (components != null))
				{
					components.Dispose();
				}
				base.Dispose(disposing);
			}
			/// <summary>
			/// Required method for Designer support - do not modify
			/// the contents of this method with the code editor.
			/// </summary>
			private void InitializeComponent()
			{
				this.components = new System.ComponentModel.Container();
				this.button1 = new System.Windows.Forms.Button();
				this.richTextBoxMessage = new System.Windows.Forms.RichTextBox();
				this.FlexibleMessageBoxFormBindingSource = new System.Windows.Forms.BindingSource(this.components);
				this.panel1 = new System.Windows.Forms.Panel();
				this.pictureBoxForIcon = new System.Windows.Forms.PictureBox();
				this.button2 = new System.Windows.Forms.Button();
				this.button3 = new System.Windows.Forms.Button();
				((System.ComponentModel.ISupportInitialize)(this.FlexibleMessageBoxFormBindingSource)).BeginInit();
				this.panel1.SuspendLayout();
				((System.ComponentModel.ISupportInitialize)(this.pictureBoxForIcon)).BeginInit();
				this.SuspendLayout();
				//
				// button1
				//
				this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
				this.button1.AutoSize = true;
				this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
				this.button1.Location = new System.Drawing.Point(11, 67);
				this.button1.MinimumSize = new System.Drawing.Size(0, 24);
				this.button1.Name = "button1";
				this.button1.Size = new System.Drawing.Size(75, 24);
				this.button1.TabIndex = 2;
				this.button1.Text = "OK";
				this.button1.UseVisualStyleBackColor = true;
				this.button1.Visible = false;
				//
				// richTextBoxMessage
				//
				this.richTextBoxMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
				| System.Windows.Forms.AnchorStyles.Left)
				| System.Windows.Forms.AnchorStyles.Right)));
				this.richTextBoxMessage.BackColor = System.Drawing.Color.White;
				this.richTextBoxMessage.BorderStyle = System.Windows.Forms.BorderStyle.None;
				this.richTextBoxMessage.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.FlexibleMessageBoxFormBindingSource, "MessageText", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
				this.richTextBoxMessage.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
				this.richTextBoxMessage.Location = new System.Drawing.Point(50, 26);
				this.richTextBoxMessage.Margin = new System.Windows.Forms.Padding(0);
				this.richTextBoxMessage.Name = "richTextBoxMessage";
				this.richTextBoxMessage.ReadOnly = true;
				this.richTextBoxMessage.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
				this.richTextBoxMessage.Size = new System.Drawing.Size(200, 20);
				this.richTextBoxMessage.TabIndex = 0;
				this.richTextBoxMessage.TabStop = false;
				this.richTextBoxMessage.Text = "<Message>";
				//
				// panel1
				//
				this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
				| System.Windows.Forms.AnchorStyles.Left)
				| System.Windows.Forms.AnchorStyles.Right)));
				this.panel1.BackColor = System.Drawing.Color.White;
				this.panel1.Controls.Add(this.pictureBoxForIcon);
				this.panel1.Controls.Add(this.richTextBoxMessage);
				this.panel1.Location = new System.Drawing.Point(-3, -4);
				this.panel1.Name = "panel1";
				this.panel1.Size = new System.Drawing.Size(268, 59);
				this.panel1.TabIndex = 1;
				//
				// pictureBoxForIcon
				//
				this.pictureBoxForIcon.BackColor = System.Drawing.Color.Transparent;
				this.pictureBoxForIcon.Location = new System.Drawing.Point(15, 19);
				this.pictureBoxForIcon.Name = "pictureBoxForIcon";
				this.pictureBoxForIcon.Size = new System.Drawing.Size(32, 32);
				this.pictureBoxForIcon.TabIndex = 8;
				this.pictureBoxForIcon.TabStop = false;
				//
				// button2
				//
				this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
				this.button2.DialogResult = System.Windows.Forms.DialogResult.OK;
				this.button2.Location = new System.Drawing.Point(92, 67);
				this.button2.MinimumSize = new System.Drawing.Size(0, 24);
				this.button2.Name = "button2";
				this.button2.Size = new System.Drawing.Size(75, 24);
				this.button2.TabIndex = 3;
				this.button2.Text = "OK";
				this.button2.UseVisualStyleBackColor = true;
				this.button2.Visible = false;
				//
				// button3
				//
				this.button3.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
				this.button3.AutoSize = true;
				this.button3.DialogResult = System.Windows.Forms.DialogResult.OK;
				this.button3.Location = new System.Drawing.Point(173, 67);
				this.button3.MinimumSize = new System.Drawing.Size(0, 24);
				this.button3.Name = "button3";
				this.button3.Size = new System.Drawing.Size(75, 24);
				this.button3.TabIndex = 0;
				this.button3.Text = "OK";
				this.button3.UseVisualStyleBackColor = true;
				this.button3.Visible = false;
				//
				// FlexibleMessageBoxForm
				//
				this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
				this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
				this.ClientSize = new System.Drawing.Size(260, 102);
				this.Controls.Add(this.button3);
				this.Controls.Add(this.button2);
				this.Controls.Add(this.panel1);
				this.Controls.Add(this.button1);
				this.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.FlexibleMessageBoxFormBindingSource, "CaptionText", true));
				this.MaximizeBox = false;
				this.MinimizeBox = false;
				this.MinimumSize = new System.Drawing.Size(276, 140);
				this.Name = "FlexibleMessageBoxForm";
				this.ShowIcon = false;
				this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
				this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
				this.Text = "<Caption>";
				this.Shown += new System.EventHandler(this.FlexibleMessageBoxForm_Shown);
				((System.ComponentModel.ISupportInitialize)(this.FlexibleMessageBoxFormBindingSource)).EndInit();
				this.panel1.ResumeLayout(false);
				((System.ComponentModel.ISupportInitialize)(this.pictureBoxForIcon)).EndInit();
				this.ResumeLayout(false);
				this.PerformLayout();
			}
			private System.Windows.Forms.Button button1;
			private System.Windows.Forms.BindingSource FlexibleMessageBoxFormBindingSource;
			private System.Windows.Forms.RichTextBox richTextBoxMessage;
			private System.Windows.Forms.Panel panel1;
			private System.Windows.Forms.PictureBox pictureBoxForIcon;
			private System.Windows.Forms.Button button2;
			private System.Windows.Forms.Button button3;
			#endregion
			#region Private constants
			//These separators are used for the "copy to clipboard" standard operation, triggered by Ctrl + C (behavior and clipboard format is like in a standard MessageBox)
			private static readonly String STANDARD_MESSAGEBOX_SEPARATOR_LINES = "---------------------------\n";
			private static readonly String STANDARD_MESSAGEBOX_SEPARATOR_SPACES = "   ";
			//These are the possible buttons (in a standard MessageBox)
			private enum ButtonID { Ok = 0, Cancel, Yes, No, Abort, Retry, Ignore };
			#endregion
			#region Private members
			private MessageBoxDefaultButton _defaultButton;
			private int _visibleButtonsCount;
			#endregion
			#region Private constructor
			/// <summary>
			/// Initializes a new instance of the <see cref="FlexibleMessageBoxForm"/> class.
			/// </summary>
			private FlexibleMessageBoxForm()
			{
				InitializeComponent();
				this.KeyPreview = true;
				this.KeyUp += FlexibleMessageBoxForm_KeyUp;
			}
			#endregion
			#region Private helper functions
			/// <summary>
			/// Gets the button text for the current UI language.
			/// Note: If the caller does not override FlexibleMessageBox.GetButtonText, then the standard
			/// L10NSharp localization approach will be used.
			/// </summary>
			/// <param name="buttonID">The ID of the button.</param>
			/// <returns>The button text</returns>
			private string GetButtonText(ButtonID buttonID)
			{
				if (FlexibleMessageBox.GetButtonText != null)
					return FlexibleMessageBox.GetButtonText("FlexibleMessageBoxBtn." + buttonID); // Constant text here must match the text used in GetButtonTextLocalizationKeys
				return GetButtonTextAsLocalizedByL10nSharp(buttonID);
			}
		
			private static string GetButtonTextAsLocalizedByL10nSharp(ButtonID buttonId)
			{
				// If we ever want to ship standard German and Italian button names in a TMX file, here they are:
				//"&OK", "&Abbrechen", "&Ja", "&Nein", "&Abbrechen", "&Wiederholen", "&Ignorieren"
				//"&OK", "&Annulla", "&Sì", "&No", "&Interrompi", "&Riprova", "I&gnora"
				switch (buttonId)
				{
					case ButtonID.Ok: return LocalizationManager.GetString("Common.OKButton", "&OK");
					case ButtonID.Cancel: return LocalizationManager.GetString("Common.CancelButton", "&Cancel");
					case ButtonID.Yes: return LocalizationManager.GetString("Common.YesButton", "&Yes");
					case ButtonID.No: return LocalizationManager.GetString("Common.NoButton", "&No");
					case ButtonID.Abort: return LocalizationManager.GetString("Common.AbortButton", "&Abort");
					case ButtonID.Retry: return LocalizationManager.GetString("Common.RetryButton", "&Retry");
					case ButtonID.Ignore: return LocalizationManager.GetString("Common.IgnoreButton", "&Ignore");
					default:
						throw new InvalidEnumArgumentException("buttonId", (int)buttonId, typeof(ButtonID));
				}
			}

			/// <summary>
			/// Center the dialog on the current screen (in case where there is no owning form to use for centering).
			/// </summary>
			/// <param name="flexibleMessageBoxForm">The FlexibleMessageBox dialog.</param>
			/// <param name="screen">The screen to display on.</param>
			private static void CenterDialogOnScreen(FlexibleMessageBoxForm flexibleMessageBoxForm, Screen screen)
			{
				flexibleMessageBoxForm.StartPosition = FormStartPosition.Manual;
				flexibleMessageBoxForm.Left = screen.Bounds.Left + screen.Bounds.Width / 2 - flexibleMessageBoxForm.Width / 2;
				flexibleMessageBoxForm.Top = screen.Bounds.Top + screen.Bounds.Height / 2 - flexibleMessageBoxForm.Height / 2;
			}

			/// <summary>
			/// Calculate the dialog's start size (Try to auto-size width to show longest text row.)
			/// Also set the maximum dialog size.
			/// </summary>
			/// <param name="flexibleMessageBoxForm">The FlexibleMessageBox dialog.</param>
			/// <param name="text">The text (the longest text row is used to calculate the dialog width).</param>
			/// <param name="caption">The caption (this can also affect the dialog width).</param>
			/// <param name="screen">The screen the message box dialog will be displayed on</param>
			private static void SetDialogSizes(FlexibleMessageBoxForm flexibleMessageBoxForm, string text, string caption, Screen screen)
			{
				//Get rows. Exit if there are no rows to render...
				if (String.IsNullOrEmpty(text))
					return;

				var maxDlgSize = new Size(Convert.ToInt32(screen.WorkingArea.Width * MaxWidthFactor),
					Convert.ToInt32(screen.WorkingArea.Height * MaxHeightFactor));
				
				//Calculate margins
				var marginWidth = flexibleMessageBoxForm.Width - flexibleMessageBoxForm.richTextBoxMessage.Width;
				var marginHeight = flexibleMessageBoxForm.Height - flexibleMessageBoxForm.richTextBoxMessage.Height;

				var maxRtfBoxSize = new Size(maxDlgSize.Width - marginWidth, maxDlgSize.Height - marginHeight);

				// Determine text size if we were to draw in maximum area available.
				var requiredRtfBoxSize = TextRenderer.MeasureText(text, FlexibleMessageBox.Font, maxRtfBoxSize, TextFormatFlags.NoClipping);
				if (requiredRtfBoxSize.Width > maxRtfBoxSize.Width)
				{
					var height = requiredRtfBoxSize.Height;
					// Once or more text lines are going to need to wrap. Calculate required size with word-wrapping.
					if (height < maxRtfBoxSize.Height)
					{
						// We might not need a vertical scroll bar. Let's see if it still fits with word-wrapping on.
						requiredRtfBoxSize.Height = height = TextRenderer.MeasureText(text, FlexibleMessageBox.Font, maxRtfBoxSize,
							TextFormatFlags.WordBreak | TextFormatFlags.NoClipping).Height;
					}
					if (height > maxRtfBoxSize.Height)
					{
						// Text is going to be too long vertically, so we'll need a scroll-bar. Go with max size.
						flexibleMessageBoxForm.Size = maxDlgSize;
						return;
						//// Calculate one more time, this time allowing for vertical scroll bar width.
						//var usableSize = new Size(maxRtfBoxSize.Width - SystemInformation.VerticalScrollBarWidth, maxRtfBoxSize.Height);
						//// Once or more text lines are going to need to wrap. Calculate required size with word-wrapping and allowing for
						//// vertical scroll bar.
						//fullTextSize = TextRenderer.MeasureText(text, FlexibleMessageBox.Font, usableSize, TextFormatFlags.WordBreak);
					}
				}
				else
				{
					var redXButtonWidth = SystemInformation.CaptionButtonSize.Width;
					var captionWidth = TextRenderer.MeasureText(caption, SystemFonts.CaptionFont).Width;
					if (captionWidth > requiredRtfBoxSize.Width - redXButtonWidth)
						requiredRtfBoxSize.Width = Math.Min(maxRtfBoxSize.Width, captionWidth + redXButtonWidth);
				}

				//var textHeight = .Height;
				//var textWidth = Math.Max(longestTextRowWidth + SystemInformation.VerticalScrollBarWidth, captionWidth);

				flexibleMessageBoxForm.Size = new Size(requiredRtfBoxSize.Width + marginWidth, requiredRtfBoxSize.Height + marginHeight);
			}

			/// <summary>
			/// Set the dialog icon.
			/// When no icon is used: Correct placement and width of rich text box.
			/// </summary>
			/// <param name="flexibleMessageBoxForm">The FlexibleMessageBox dialog.</param>
			/// <param name="icon">The MessageBoxIcon.</param>
			private static void SetDialogIcon(FlexibleMessageBoxForm flexibleMessageBoxForm, MessageBoxIcon icon)
			{
				switch (icon)
				{
					case MessageBoxIcon.Information:
						flexibleMessageBoxForm.pictureBoxForIcon.Image = SystemIcons.Information.ToBitmap();
						break;
					case MessageBoxIcon.Warning:
						flexibleMessageBoxForm.pictureBoxForIcon.Image = SystemIcons.Warning.ToBitmap();
						break;
					case MessageBoxIcon.Error:
						flexibleMessageBoxForm.pictureBoxForIcon.Image = SystemIcons.Error.ToBitmap();
						break;
					case MessageBoxIcon.Question:
						flexibleMessageBoxForm.pictureBoxForIcon.Image = SystemIcons.Question.ToBitmap();
						break;
					default:
						//When no icon is used: Correct placement and width of rich text box.
						flexibleMessageBoxForm.pictureBoxForIcon.Visible = false;
						flexibleMessageBoxForm.richTextBoxMessage.Left -= flexibleMessageBoxForm.pictureBoxForIcon.Width;
						flexibleMessageBoxForm.richTextBoxMessage.Width += flexibleMessageBoxForm.pictureBoxForIcon.Width;
						break;
				}
			}
			/// <summary>
			/// Set dialog buttons visibilities and texts.
			/// Also set a default button.
			/// </summary>
			/// <param name="flexibleMessageBoxForm">The FlexibleMessageBox dialog.</param>
			/// <param name="buttons">The buttons.</param>
			/// <param name="defaultButton">The default button.</param>
			private static void SetDialogButtons(FlexibleMessageBoxForm flexibleMessageBoxForm, MessageBoxButtons buttons, MessageBoxDefaultButton defaultButton)
			{
				//Set the buttons visibilities and texts
				switch (buttons)
				{
					case MessageBoxButtons.AbortRetryIgnore:
						flexibleMessageBoxForm._visibleButtonsCount = 3;
						flexibleMessageBoxForm.button1.Visible = true;
						flexibleMessageBoxForm.button1.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Abort);
						flexibleMessageBoxForm.button1.DialogResult = DialogResult.Abort;
						flexibleMessageBoxForm.button2.Visible = true;
						flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Retry);
						flexibleMessageBoxForm.button2.DialogResult = DialogResult.Retry;
						flexibleMessageBoxForm.button3.Visible = true;
						flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Ignore);
						flexibleMessageBoxForm.button3.DialogResult = DialogResult.Ignore;
						flexibleMessageBoxForm.ControlBox = false;
						break;
					case MessageBoxButtons.OKCancel:
						flexibleMessageBoxForm._visibleButtonsCount = 2;
						flexibleMessageBoxForm.button2.Visible = true;
						flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Ok);
						flexibleMessageBoxForm.button2.DialogResult = DialogResult.OK;
						flexibleMessageBoxForm.button3.Visible = true;
						flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Cancel);
						flexibleMessageBoxForm.button3.DialogResult = DialogResult.Cancel;
						flexibleMessageBoxForm.CancelButton = flexibleMessageBoxForm.button3;
						break;
					case MessageBoxButtons.RetryCancel:
						flexibleMessageBoxForm._visibleButtonsCount = 2;
						flexibleMessageBoxForm.button2.Visible = true;
						flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Retry);
						flexibleMessageBoxForm.button2.DialogResult = DialogResult.Retry;
						flexibleMessageBoxForm.button3.Visible = true;
						flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Cancel);
						flexibleMessageBoxForm.button3.DialogResult = DialogResult.Cancel;
						flexibleMessageBoxForm.CancelButton = flexibleMessageBoxForm.button3;
						break;
					case MessageBoxButtons.YesNo:
						flexibleMessageBoxForm._visibleButtonsCount = 2;
						flexibleMessageBoxForm.button2.Visible = true;
						flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Yes);
						flexibleMessageBoxForm.button2.DialogResult = DialogResult.Yes;
						flexibleMessageBoxForm.button3.Visible = true;
						flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.No);
						flexibleMessageBoxForm.button3.DialogResult = DialogResult.No;
						flexibleMessageBoxForm.ControlBox = false;
						break;
					case MessageBoxButtons.YesNoCancel:
						flexibleMessageBoxForm._visibleButtonsCount = 3;
						flexibleMessageBoxForm.button1.Visible = true;
						flexibleMessageBoxForm.button1.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Yes);
						flexibleMessageBoxForm.button1.DialogResult = DialogResult.Yes;
						flexibleMessageBoxForm.button2.Visible = true;
						flexibleMessageBoxForm.button2.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.No);
						flexibleMessageBoxForm.button2.DialogResult = DialogResult.No;
						flexibleMessageBoxForm.button3.Visible = true;
						flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Cancel);
						flexibleMessageBoxForm.button3.DialogResult = DialogResult.Cancel;
						flexibleMessageBoxForm.CancelButton = flexibleMessageBoxForm.button3;
						break;
					case MessageBoxButtons.OK:
					default:
						flexibleMessageBoxForm._visibleButtonsCount = 1;
						flexibleMessageBoxForm.button3.Visible = true;
						flexibleMessageBoxForm.button3.Text = flexibleMessageBoxForm.GetButtonText(ButtonID.Ok);
						flexibleMessageBoxForm.button3.DialogResult = DialogResult.OK;
						flexibleMessageBoxForm.CancelButton = flexibleMessageBoxForm.button3;
						break;
				}
				//Set default button (used in FlexibleMessageBoxForm_Shown)
				flexibleMessageBoxForm._defaultButton = defaultButton;
			}
			#endregion
			#region Private event handlers
			/// <summary>
			/// Handles the Shown event of the FlexibleMessageBoxForm control.
			/// </summary>
			/// <param name="sender">The source of the event.</param>
			/// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
			private void FlexibleMessageBoxForm_Shown(object sender, EventArgs e)
			{
				int buttonIndexToFocus;
				Button buttonToFocus;
				switch (_defaultButton)
				{
					default:
						buttonIndexToFocus = 1;
						break;
					case MessageBoxDefaultButton.Button2:
						buttonIndexToFocus = 2;
						break;
					case MessageBoxDefaultButton.Button3:
						buttonIndexToFocus = 3;
						break;
				}
				if (buttonIndexToFocus > _visibleButtonsCount)
					buttonIndexToFocus = _visibleButtonsCount;

				switch (buttonIndexToFocus)
				{
					case 3:
						buttonToFocus = button3;
						break;
					case 2:
						buttonToFocus = button2;
						break;
					default:
						buttonToFocus = button1;
						break;
				}
				buttonToFocus.Focus();
			}
			/// <summary>
			/// Handles the KeyUp event of the richTextBoxMessage control.
			/// </summary>
			/// <param name="sender">The source of the event.</param>
			/// <param name="e">The <see cref="System.Windows.Forms.KeyEventArgs"/> instance containing the event data.</param>
			void FlexibleMessageBoxForm_KeyUp(object sender, KeyEventArgs e)
			{
				//Handle standard key strikes for clipboard copy: "Ctrl + C" and "Ctrl + Insert"
				if (e.Control && (e.KeyCode == Keys.C || e.KeyCode == Keys.Insert))
				{
					var buttonsTextLine = (this.button1.Visible ? this.button1.Text + STANDARD_MESSAGEBOX_SEPARATOR_SPACES : string.Empty)
										+ (this.button2.Visible ? this.button2.Text + STANDARD_MESSAGEBOX_SEPARATOR_SPACES : string.Empty)
										+ (this.button3.Visible ? this.button3.Text + STANDARD_MESSAGEBOX_SEPARATOR_SPACES : string.Empty);
					//Build same clipboard text like the standard .Net MessageBox
					var textForClipboard = STANDARD_MESSAGEBOX_SEPARATOR_LINES
										 + this.Text + Environment.NewLine
										 + STANDARD_MESSAGEBOX_SEPARATOR_LINES
										 + this.richTextBoxMessage.Text + Environment.NewLine
										 + STANDARD_MESSAGEBOX_SEPARATOR_LINES
										 + buttonsTextLine.Replace("&", string.Empty) + Environment.NewLine
										 + STANDARD_MESSAGEBOX_SEPARATOR_LINES;
					//Set text in clipboard
					Clipboard.SetText(textForClipboard);
				}
			}
			#endregion

			#region Properties (only used for binding)
			/// <summary>
			/// The text that is been used for the heading.
			/// </summary>
			public string CaptionText { get; set; }
			/// <summary>
			/// The text that is been used in the FlexibleMessageBoxForm.
			/// </summary>
			public string MessageText { get; set; }
			#endregion

			#region Public show function
			/// <summary>
			/// Shows the specified message box.
			/// </summary>
			/// <param name="owner">The owner.</param>
			/// <param name="text">The text.</param>
			/// <param name="caption">The caption.</param>
			/// <param name="buttons">The buttons.</param>
			/// <param name="icon">The icon.</param>
			/// <param name="defaultButton">The default button.</param>
			/// <param name="linkClickedAction">optional handler if user clicks a hyperlink (as determined by RichTextBox). Set to
			/// <seealso cref="BasicLinkClickedEventHandler"/> to get basic handling. If <c>null</c>, URLs will not be detected or
			/// highlighted in the message.</param>
			/// <returns>The dialog result.</returns>
			/// <returns>The dialog result.</returns>
			/// <exception cref="Exception">Exceptions might be thrown by the <paramref name="linkClickedAction"/></exception>
			internal static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons,
				MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, LinkClickedEventHandler linkClickedAction = null)
			{
				// Create a new instance of the FlexibleMessageBox form
				var flexibleMessageBoxForm = new FlexibleMessageBoxForm();
				flexibleMessageBoxForm.ShowInTaskbar = false;
				// Bind the caption and the message text
				flexibleMessageBoxForm.CaptionText = caption;
				flexibleMessageBoxForm.MessageText = text;
				flexibleMessageBoxForm.FlexibleMessageBoxFormBindingSource.DataSource = flexibleMessageBoxForm;
				// Set the buttons visibilities and texts. Also set a default button.
				SetDialogButtons(flexibleMessageBoxForm, buttons, defaultButton);
				// Set the dialogs icon. When no icon is used, correct placement and width of rich text box.
				SetDialogIcon(flexibleMessageBoxForm, icon);
				// Set the font for all controls
				flexibleMessageBoxForm.Font = FlexibleMessageBox.Font;
				flexibleMessageBoxForm.richTextBoxMessage.Font = FlexibleMessageBox.Font;
				if (linkClickedAction == null)
					flexibleMessageBoxForm.richTextBoxMessage.DetectUrls = false;
				else
					flexibleMessageBoxForm.richTextBoxMessage.LinkClicked += linkClickedAction;

				var screen = owner == null ? Screen.FromPoint(Cursor.Position) : Screen.FromHandle(owner.Handle);
				// Calculate the dialog's start size (Try to auto-size width to show longest text row). Also set the maximum dialog size.
				SetDialogSizes(flexibleMessageBoxForm, text, caption, screen);
				// If an owning window was supplied, the message box dialog is initially displayed using the default "Center Parent".
				// Otherwise, we center it on the current screen.
				if (owner == null)
					CenterDialogOnScreen(flexibleMessageBoxForm, screen);
				// Show the dialog
				return flexibleMessageBoxForm.ShowDialog(owner);
			}
			#endregion
		} //class FlexibleMessageBoxForm
		#endregion
	}
}