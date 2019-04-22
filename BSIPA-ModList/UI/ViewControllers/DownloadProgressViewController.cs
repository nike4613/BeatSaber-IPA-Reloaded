using System;
using System.Collections.Generic;
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

        private Button _checkForUpdates;
        private Button _downloadUpdates;
        private TableView _currentlyUpdatingTableView;
        private LevelListTableCell _songListTableCellInstance;
        private Button _pageUpButton;
        private Button _pageDownButton;

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

                _pageUpButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().Last(x => (x.name == "PageUpButton")), rectTransform, false);
                (_pageUpButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 1f);
                (_pageUpButton.transform as RectTransform).anchoredPosition = new Vector2(0f, -14f);
                (_pageUpButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                _pageUpButton.interactable = true;
                _pageUpButton.onClick.AddListener(delegate ()
                {
                    _currentlyUpdatingTableView.PageScrollUp();
                });

                _pageDownButton = Instantiate(Resources.FindObjectsOfTypeAll<Button>().First(x => (x.name == "PageDownButton")), rectTransform, false);
                (_pageDownButton.transform as RectTransform).anchorMin = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchorMax = new Vector2(0.5f, 0f);
                (_pageDownButton.transform as RectTransform).anchoredPosition = new Vector2(0f, 8f);
                (_pageDownButton.transform as RectTransform).sizeDelta = new Vector2(40f, 10f);
                _pageDownButton.interactable = true;
                _pageDownButton.onClick.AddListener(delegate ()
                {
                    _currentlyUpdatingTableView.PageScrollDown();
                });

                var gobj = new GameObject("DownloadTable");
                gobj.SetActive(false);
                _currentlyUpdatingTableView = gobj.AddComponent<TableView>();
                _currentlyUpdatingTableView.transform.SetParent(rectTransform, false);

                _currentlyUpdatingTableView.SetPrivateField("_isInitialized", false);
                _currentlyUpdatingTableView.SetPrivateField("_preallocatedCells", new TableView.CellsGroup[0]);
                _currentlyUpdatingTableView.Init();

                RectMask2D viewportMask = Instantiate(Resources.FindObjectsOfTypeAll<RectMask2D>().First(), _currentlyUpdatingTableView.transform, false);
                viewportMask.transform.DetachChildren();
                _currentlyUpdatingTableView.GetComponentsInChildren<RectTransform>().First(x => x.name == "Content").transform.SetParent(viewportMask.rectTransform, false);

                (_currentlyUpdatingTableView.transform as RectTransform).anchorMin = new Vector2(0.3f, 0.5f);
                (_currentlyUpdatingTableView.transform as RectTransform).anchorMax = new Vector2(0.7f, 0.5f);
                (_currentlyUpdatingTableView.transform as RectTransform).sizeDelta = new Vector2(0f, 60f);
                (_currentlyUpdatingTableView.transform as RectTransform).anchoredPosition = new Vector3(0f, -3f);

                ReflectionUtil.SetPrivateField(_currentlyUpdatingTableView, "_pageUpButton", _pageUpButton);
                ReflectionUtil.SetPrivateField(_currentlyUpdatingTableView, "_pageDownButton", _pageDownButton);

                _currentlyUpdatingTableView.selectionType = TableViewSelectionType.None;
                _currentlyUpdatingTableView.dataSource = this;
                gobj.SetActive(true);

                _checkForUpdates = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton", new Vector2(36f, -30f), new Vector2(20f, 10f), CheckUpdates, "Check for updates");
                _checkForUpdates.interactable = DownloadController.Instance.CanCheck || DownloadController.Instance.CanReset;
                _checkForUpdates.ToggleWordWrapping(false);

                _downloadUpdates = BeatSaberUI.CreateUIButton(rectTransform, "CreditsButton", new Vector2(36f, -15f), new Vector2(20f, 10f), DownloadUpdates, "Download Updates");
                _downloadUpdates.interactable = DownloadController.Instance.CanDownload;
                _downloadUpdates.ToggleWordWrapping(false);

                DownloadController.Instance.OnDownloaderListChanged += Refresh;
                DownloadController.Instance.OnDownloadStateChanged += DownloaderStateChanged;
            }
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
            _checkForUpdates.interactable = DownloadController.Instance.CanCheck || DownloadController.Instance.CanReset;
            _downloadUpdates.interactable = DownloadController.Instance.CanDownload;
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
            return 10f;
        }

        public int NumberOfCells()
        {
            return DownloadController.Instance.Downloads.Count;
        }

        public TableCell CellForIdx(int row)
        {
            LevelListTableCell _tableCell = Instantiate(_songListTableCellInstance);
            DownloadProgressCell _queueCell = _tableCell.gameObject.AddComponent<DownloadProgressCell>();
            _queueCell.Init(DownloadController.Instance.Downloads[row]);
            return _queueCell;
        }
    }
}
