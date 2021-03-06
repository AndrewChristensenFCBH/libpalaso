﻿using System;
using System.Windows.Forms;
using SIL.Reporting;

namespace SIL.Windows.Forms.ImageToolbox
{
	public partial class ImageToolboxDialog : Form
	{
		/// <summary>
		///
		/// </summary>
		/// <param name="imageInfo">optional (can be null)</param>
		/// <param name="initialSearchString">optional</param>
		public ImageToolboxDialog(PalasoImage imageInfo, string initialSearchString)
		{
			InitializeComponent();
			_imageToolboxControl.ImageInfo = imageInfo;
			_imageToolboxControl.InitialSearchString = initialSearchString;
			SearchLanguage = "en";	// unless the caller specifies otherwise explicitly
		}
		public PalasoImage ImageInfo { get { return _imageToolboxControl.ImageInfo; } }

		/// <summary>
		/// Sets the language used in searching for an image by words.
		/// </summary>
		public string SearchLanguage
		{
			get { return _imageToolboxControl.SearchLanguage; }
			set { _imageToolboxControl.SearchLanguage = value; }
		}

		private void _okButton_Click(object sender, EventArgs e)
		{
			//enhance: doesn't tell us all that much.
			UsageReporter.SendNavigationNotice("ImageToolboxDialog/Ok");
			DialogResult = (ImageInfo==null || ImageInfo.Image==null)? DialogResult.Cancel : DialogResult.OK;
			_imageToolboxControl.Closing();
			Close();
		}

		private void _cancelButton_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			_imageToolboxControl.Closing();
			Close();
		}

		private void ImageToolboxDialog_Load(object sender, EventArgs e)
		{
			UsageReporter.SendNavigationNotice("ImageToolbox");
		}
	}
}
