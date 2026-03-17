using System;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.ExcelSync.Editor.DataBrowser
{
    [Serializable]
    public sealed class DataBrowserPager
    {
        public int CurrentPage { get; private set; }
        public int PageSize { get; private set; } = 200;

        public void SetPageSize(int size)
        {
            PageSize = Math.Max(1, size);
            ClampToTotal(int.MaxValue);
        }

        public void ResetToFirstPage()
        {
            CurrentPage = 0;
        }

        public void SetCurrentPage(int page)
        {
            CurrentPage = Math.Max(0, page);
        }

        public void ClampToTotal(int total)
        {
            if (total <= 0)
            {
                CurrentPage = 0;
                return;
            }

            var pageCount = (total + PageSize - 1) / PageSize;
            CurrentPage = Math.Max(0, Math.Min(CurrentPage, pageCount - 1));
        }

        public void GetRange(int total, out int start, out int end)
        {
            ClampToTotal(total);
            start = CurrentPage * PageSize;
            end = Math.Min(total, start + PageSize);
        }

        public void DrawGUI(int total, Action onPageChanged)
        {
            ClampToTotal(total);
            var pageCount = total <= 0 ? 1 : (total + PageSize - 1) / PageSize;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Page {CurrentPage + 1}/{pageCount}", GUILayout.Width(100));

                EditorGUI.BeginDisabledGroup(CurrentPage <= 0);
                if (GUILayout.Button("Prev", GUILayout.Width(60)))
                {
                    CurrentPage--;
                    onPageChanged?.Invoke();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(CurrentPage >= pageCount - 1);
                if (GUILayout.Button("Next", GUILayout.Width(60)))
                {
                    CurrentPage++;
                    onPageChanged?.Invoke();
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("PageSize", GUILayout.Width(60));
                var newSize = EditorGUILayout.IntField(PageSize, GUILayout.Width(70));
                if (newSize != PageSize)
                {
                    SetPageSize(newSize);
                    onPageChanged?.Invoke();
                }
            }
        }
    }
}
