using System;
using CadAutomation.Core.Abstractions;
using Inv = Inventor;

namespace CadAutomation.Inventor.Adapters
{
    /// <summary>
    /// IActiveAssemblyProvider'ın Inventor implementasyonu - UI panelinin CAD'den bağımsız kalması
    /// için Inventor.Application referansını burada, tek noktada tutar.
    /// </summary>
    public sealed class InventorActiveAssemblyProvider : IActiveAssemblyProvider
    {
        private readonly Inv.Application _application;

        public InventorActiveAssemblyProvider(Inv.Application application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
        }

        public ICadAssemblyRoot? GetActiveAssembly()
        {
            if (_application.ActiveDocument is Inv.AssemblyDocument assemblyDocument)
            {
                return new InventorCadAssemblyRoot(assemblyDocument);
            }
            return null;
        }
    }
}
