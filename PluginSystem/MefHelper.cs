using System;
using System.Reflection;
using System.IO;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Collections.Generic;

// Template version 1.2.0.2. Code developed for framework v2.0.50727.3074
// This code is copyright (c) 2009 Computer DJ. All rights reserved.

/// <summary>
/// Managed Extensibility Framework Helper Class
/// </summary>
/// <remarks>
/// Singleton object
/// -- Change Log --------------------------------------------------------
/// 4/5/2009 11:55:06 PM by Rick - Initial Creation
/// 7/15/2009 10:00:00 PM by Rick - Changed to MEF Preview 6 (Net 4.0)
/// 
/// </remarks>
public sealed class MefHelper : IDisposable
{

    #region Constructor / Deconstructor

    /// <summary>
    /// The starting point for this class
    /// </summary>
    /// <remarks></remarks>
    private MefHelper()
    {

        string sPathInitial = string.Empty;

        sPathInitial = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        Assembly ass = Assembly.GetEntryAssembly();

        _ExtensionsPath = Path.Combine(sPathInitial, ass.GetName().Name);

        try
        {
            if (Directory.Exists(_ExtensionsPath) == false)
                Directory.CreateDirectory(_ExtensionsPath);
        }
        catch (Exception ex)
        {
        }

        _ExtensionsPath = Path.Combine(_ExtensionsPath, "Extensions");

        try
        {
            if (Directory.Exists(_ExtensionsPath) == false)
                Directory.CreateDirectory(_ExtensionsPath);
        }
        catch (Exception ex)
        {
        }

        #if (DEBUG)
        _ExtensionsPath = Path.Combine(Directory.GetParent(Directory.GetParent(Directory.GetParent(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).ToString()).ToString()).ToString()).ToString(), @"VCNEditor\bin\Debug");
        #endif
    }

    #region IDisposable Members

    void IDisposable.Dispose()
    {
        _Container.Dispose();
        _Container = null;
    }

    #endregion

    #endregion

    #region Public

    #region Public Property Declarations

    private CompositionContainer _Container = null;
    public CompositionContainer Container
    {
        get { return _Container; }
    }

    private string _ExtensionsPath = "";
    public string ExtensionsPath
    {
        get { return _ExtensionsPath; }
        set
        {
            if (!(_ExtensionsPath == value))
            {
                _ExtensionsPath = value;
            }
        }
    }

    #endregion

    #region Public Static Property Declarations

    // Properties
    public static MefHelper Instance
    {
        get
        {
            if ((MefHelper._instance == null))
            {
                lock (MefHelper.syncRoot)
                {
                    if ((MefHelper._instance == null))
                    {
                        MefHelper._instance = new MefHelper();
                    }
                }
            }

            return MefHelper._instance;
        }
    }

    // Fields
    //ModReq(IsVolatile)
    private static MefHelper _instance;

    private static object syncRoot = new object();
    #endregion

    #region Public Methods

    /// <summary>
    /// Composes the application and all MEF attributed components
    /// </summary>
    /// <remarks></remarks>

    public void Compose()
    {
        AggregateCatalog Catalog = new AggregateCatalog();

        // Add This assembly's catalog parts
        System.Reflection.Assembly ass = System.Reflection.Assembly.GetEntryAssembly();
        Catalog.Catalogs.Add(new AssemblyCatalog(ass));

        // Directory of catalog parts
        if (System.IO.Directory.Exists(ExtensionsPath))
        {
            Catalog.Catalogs.Add(new DirectoryCatalog(ExtensionsPath));
            string[] folders = System.IO.Directory.GetDirectories(ExtensionsPath);

            foreach (string folder in folders)
            {
                Catalog.Catalogs.Add(new DirectoryCatalog(folder));
            }

        }

        _Container = new CompositionContainer(Catalog);
    }

    #endregion

    #endregion

}
