using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using HMUI;
using IPA.Updating.BeatMods;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRUI;

namespace BSIPA_ModList.UI.ViewControllers
{
    // originally ripped verbatim from Andruzz's BeatSaverDownloader
    internal class DownloadProgressViewController : VRUIViewController, TableView.IDataSource
    {
        private TextMeshProUGUI _titleText;
        private TextMeshProUGUI _manualDownloadText;

        private Button _checkForUpdates;
        private Button _downloadUpdates;
        private Button _restartGame;
        private TableView _currentlyUpdatingTableView;
        private LevelListTableCell _songListTableCellInstance;
        private Button _pageUpButton;
        private Button _pageDownButton;
        private TableViewScroller _scroller;

        private const float TableXOffset = -20f;
        private const float ButtonXOffset = 36f;
        private static readonly Vector2 ButtonSize = new Vector2(40f, 10f);

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            if (firstActivation && type == ActivationType.AddedToHierarchy)
            {
                DownloadController.Instance.OnDownloaderListChanged -= Refresh;
                DownloadController.Instance.OnDownloadStateChanged -= DownloaderStateChanged;

                _songListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                _titleText = BeatSaberUI.CreateText(rectTransform, "DOWNLOAD QUEUE", new Vector2(0f, 35f));
                _titleText.alignment = TextAlignmentOptions.Top;
                _titleText.fontSize = 6f;

                _manualDownloadText = BeatSaberUI.CreateText(rectTransform, "Manual Restart Required", new Vector2(37f, -3f));
                _manualDownloadText.alignment = TextAlignmentOptions.Top;
                _manualDownloadText.fontSize = 4f;
                _manualDownloadText.gameObject.SetActive(false);

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(TableXOffset, -14f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _scroller.PageScrollUp();
                });

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(TableXOffset, 8f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _scroller.PageScrollDown();
                });

                var gobj = new GameObject("DownloadTable");
                gobj.SetActive(false);
                _currentlyUpdatingTableView = gobj.AddComponent<TableView>();
                _currentlyUpdatingTableView.transform.SetParent(rectTransform, false);

                _currentlyUpdatingTableView.SetPrivateField("_isInitialized", false);
                _currentlyUpdatingTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);

                var viewport = new GameObject("Viewport").AddComponent<RectTransform>();
                viewport.SetParent(gobj.GetComponent<RectTransform>(), false);
                gobj.GetComponent<ScrollRect>().viewport = viewport;

                RectMask2D viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<RectMask2D>().First(), _currentlyUpdatingTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _currentlyUpdatingTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_currentlyUpdatingTableView.transform as RectTransform).anchorMin = new Vector2(0.3f, 0.5f);
                (_currentlyUpdatingTableView.transform as RectTransform).anchorMax = new Vector2(0.7f, 0.5f);
                (_currentlyUpdatingTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_currentlyUpdatingTableView.transform as RectTransform).anchoredPosition = new Vector3(TableXOffset, -3f);

                ReflectionUtil.SetPrivateField(_currentlyUpdatingTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_currentlyUpdatingTableView, "_pageDownButton", _pageDownButton);

                _currentlyUpdatingTableView.selectionType = TableViewSelectionType.None;

                _currentlyUpdatingTableView.Init();
                _currentlyUpdatingTableView.dataSource = this; // calls Init
                _scroller = gobj.GetComponent<TableViewScroller>();
                gobj.SetActive(true);

                _currentlyUpdatingTableView.RefreshScrollButtons();

                _checkForUpdates = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton", new Vector2(ButtonXOffset, -30f), ButtonSize, CheckUpdates, "Check for updates");
                _checkForUpdates.interactable = DownloadController.Instance.CanCheck || DownloadController.Instance.CanReset;
                _checkForUpdates.ToggleWordWrapping(false);

                _downloadUpdates = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton", new Vector2(ButtonXOffset, -19f), ButtonSize, DownloadUpdates, "Download Updates");
                _downloadUpdates.interactable = DownloadController.Instance.CanDownload;
                _downloadUpdates.ToggleWordWrapping(false);

                _restartGame = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton", new Vector2(ButtonXOffset, -8f), ButtonSize, Restart, "Restart Game");
                _restartGame.interactable = DownloadController.Instance.HadUpdates;
                _restartGame.ToggleWordWrapping(false);
            }

            DownloadController.Instance.OnDownloaderListChanged += Refresh;
            DownloadController.Instance.OnDownloadStateChanged += DownloaderStateChanged;

            DownloaderStateChanged();
        }

        private void Restart()
        {
            Process.Start(Path.Combine(Environment.CurrentDirectory, Process.GetCurrentProcess().MainModule.FileName), Environment.CommandLine);
            Application.Quit();
        }

        private void DownloadUpdates()
        {
            if (DownloadController.Instance.CanDownload)
                DownloadController.Instance.StartDownloads();
        }

        private void CheckUpdates()
        {
            Updater.ResetRequestCache();
            if (DownloadController.Instance.CanReset)
                DownloadController.Instance.ResetCheck(false);
            if (DownloadController.Instance.CanCheck)
                DownloadController.Instance.CheckForUpdates();
        }

        private void DownloaderStateChanged()
        {
            _checkForUpdates.interactable = (DownloadController.Instance.CanCheck || DownloadController.Instance.CanReset) && !Updater.NeedsManualRestart;
            _downloadUpdates.interactable = DownloadController.Instance.CanDownload && !Updater.NeedsManualRestart;
            _restartGame.interactable = DownloadController.Instance.HadUpdates && !Updater.NeedsManualRestart;
            _manualDownloadText.gameObject.SetActive(Updater.NeedsManualRestart);
        }

        protected override void DidDeactivate(DeactivationType type)
        {
            DownloadController.Instance.OnDownloaderListChanged -= Refresh;
            DownloadController.Instance.OnDownloadStateChanged -= DownloaderStateChanged;

            if (DownloadController.Instance.IsDone)
                FloatingNotification.instance.Close();
        }

        private void Refresh()
        {
            _currentlyUpdatingTableView.ReloadData();
        }

        public float CellSize()
        {
            return 8.5f;
        }

        public int NumberOfCells()
        {
            return DownloadController.Instance.Downloads.Count;
        }

        public TableCell CellForIdx(TableView view, int row)
        {
            LevelListTableCell _tableCell = Instantiate(_songListTableCellInstance);
            DownloadProgressCell _queueCell = _tableCell.gameObject.AddComponent<DownloadProgressCell>();
            _queueCell.Init(DownloadController.Instance.Downloads[row]);
            return _queueCell;
        }
    }
}
