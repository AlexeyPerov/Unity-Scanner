using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Export;

namespace UnityScanner.Core.Categories
{
    public class UnityScannerCategoryRegistry
    {
        private readonly Dictionary<string, IUnityScannerCategory> _categories = new Dictionary<string, IUnityScannerCategory>();
        private readonly Dictionary<string, IUnityScannerFixProvider> _fixProviders = new Dictionary<string, IUnityScannerFixProvider>();
        private readonly List<IUnityScannerExporter> _exporters = new List<IUnityScannerExporter>();
        private List<IUnityScannerCategory> _cachedCategoriesList;

        public IReadOnlyList<IUnityScannerCategory> Categories
        {
            get
            {
                if (_cachedCategoriesList == null)
                    _cachedCategoriesList = new List<IUnityScannerCategory>(_categories.Values);
                return _cachedCategoriesList;
            }
        }
        public IReadOnlyList<IUnityScannerExporter> Exporters => _exporters;

        public void RegisterCategory(IUnityScannerCategory category)
        {
            _categories[category.Id] = category;
            _cachedCategoriesList = null;
        }

        public void RegisterFixProvider(string categoryId, IUnityScannerFixProvider provider)
        {
            _fixProviders[categoryId] = provider;
        }

        public void RegisterExporter(IUnityScannerExporter exporter)
        {
            _exporters.Add(exporter);
        }

        public IUnityScannerCategory GetCategory(string id)
        {
            _categories.TryGetValue(id, out var category);
            return category;
        }

        public IUnityScannerFixProvider GetFixProvider(string categoryId)
        {
            _fixProviders.TryGetValue(categoryId, out var provider);
            return provider;
        }

        public IUnityScannerExporter GetExporter(string formatId)
        {
            return _exporters.FirstOrDefault(e => e.FormatId == formatId);
        }

        public void Clear()
        {
            _categories.Clear();
            _fixProviders.Clear();
            _exporters.Clear();
            _cachedCategoriesList = null;
        }
    }
}
