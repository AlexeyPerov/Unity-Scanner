using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Progress;
using UnityScanner.Core.Results;

namespace UnityScanner.Core.Categories
{
    public class UnityScannerOrchestrator
    {
        private readonly UnityScannerCategoryRegistry _registry;
        private bool _cancelled;

        public bool IsRunning { get; private set; }
        public event Action<UnityScannerProgressInfo> OnProgress;
        public event Action<int, int, string> OnCategoryStarted;

        public UnityScannerOrchestrator(UnityScannerCategoryRegistry registry)
        {
            _registry = registry;
        }

        public void Cancel()
        {
            _cancelled = true;
        }

        public IEnumerator RunAll(UnityScannerScanContext context, Action<UnityScannerAggregateResult> onComplete)
        {
            if (IsRunning) yield break;
            IsRunning = true;
            _cancelled = false;

            var aggregate = new UnityScannerAggregateResult();
            var totalSw = Stopwatch.StartNew();

            var enabledCategories = new List<IUnityScannerCategory>();
            foreach (var cat in _registry.Categories)
                if (cat.Settings.Enabled)
                    enabledCategories.Add(cat);

            var totalCount = enabledCategories.Count;
            for (var i = 0; i < enabledCategories.Count; i++)
            {
                if (_cancelled) break;
                var category = enabledCategories[i];
                OnCategoryStarted?.Invoke(i + 1, totalCount, category.DisplayName);
                context.PreviousResults = aggregate;
                yield return RunCategory(category, context, aggregate);
            }

            totalSw.Stop();
            aggregate.TotalDurationMs = totalSw.Elapsed.TotalMilliseconds;
            aggregate.Cancelled = _cancelled;

            IsRunning = false;
            onComplete?.Invoke(aggregate);
        }

        public IEnumerator RunCategory(string categoryId, UnityScannerScanContext context, Action<UnityScannerResult> onComplete)
        {
            var category = _registry.GetCategory(categoryId);
            if (category == null) yield break;

            var aggregate = new UnityScannerAggregateResult();
            yield return RunCategory(category, context, aggregate);

            if (aggregate.Results.Count > 0)
            {
                onComplete?.Invoke(aggregate.Results[0]);
            }
        }

        private IEnumerator RunCategory(IUnityScannerCategory category, UnityScannerScanContext context, UnityScannerAggregateResult aggregate)
        {
            var result = new UnityScannerResult
            {
                CategoryId = category.Id,
                DisplayName = category.DisplayName,
                ShortDisplayName = category.ShortDisplayName,
                Capabilities = category.Capabilities
            };

            var sink = new UnityScannerIssueSink();
            sink.OnProgressUpdated += info =>
            {
                info.CategoryId = category.Id;
                OnProgress?.Invoke(info);
            };

            var sw = Stopwatch.StartNew();

            IEnumerator enumerator = null;
            try
            {
                enumerator = category.Scan(context, sink);
            }
            catch (Exception ex)
            {
                result.Succeeded = false;
                result.ErrorMessage = "[" + ex.Message + "] . See logs for details.";
                UnityEngine.Debug.LogException(ex);
            }

            if (enumerator != null)
            {
                while (true)
                {
                    bool moved;
                    try
                    {
                        moved = enumerator.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        result.Succeeded = false;
                        result.ErrorMessage = "[" + ex.Message + "] . See logs for details.";
                        UnityEngine.Debug.LogException(ex);
                        break;
                    }

                    if (!moved || _cancelled) break;
                    yield return enumerator.Current;
                }
            }

            sw.Stop();
            result.Issues = new List<UnityScannerIssue>(sink.Issues);
            result.ScanDurationMs = sw.Elapsed.TotalMilliseconds;

            if (sink is UnityScannerIssueSink concreteSink && concreteSink.WasSkipped)
            {
                result.Skipped = true;
                result.SkipReason = concreteSink.SkipReason;
                result.Succeeded = true;
            }

            aggregate.Results.Add(result);
        }
    }
}
