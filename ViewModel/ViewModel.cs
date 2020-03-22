﻿using System;
using ViewModel.Helper;

namespace ViewModel
{
    public class ViewModel
    {
        public ViewModel()
        {
            Model = new Model.ModelClass();
            DownloadButton = new ActionCommand(DownloadButtonCommand);
            DownloadLink = new ActionCommand(DownloadLinkButtonCommand);
            HelpButton = new ActionCommand(HelpButtonCommand);
            FolderButton = new ActionCommand(FolderButtonCommand);
        }
        public Model.ModelClass Model { get; set; }
        public ActionCommand DownloadButton { get; set; }
        public ActionCommand DownloadLink { get; set; }
        public ActionCommand HelpButton { get;  set; }
        public ActionCommand FolderButton { get; private set; }

        private void DownloadButtonCommand()
        {
            Model.DownloadButtonClick();
        }
        private void HelpButtonCommand()
        {
            Model.HelpButtonClick();
        }

        private void FolderButtonCommand()
        {
            Model.FolderButtonClick();
        }
        private void DownloadLinkButtonCommand()
        {
            Model.DownloadLinkButtonClick();
        }

    }
}
