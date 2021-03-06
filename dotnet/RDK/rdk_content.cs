#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

#if RDK_UNCHECKED

namespace Rhino.Render
{
  [Flags]
  public enum RenderContentStyles : int
  {
    /// <summary>
    /// No defined styles
    /// </summary>
    None = 0,
    /// <summary>
    /// Texture UI includes an auto texture summary section. See AddAutoParameters().
    /// </summary>
    TextureSummary = 0x0001,
    /// <summary>
    /// Editor displays an instant preview before preview cycle begins.
    /// </summary>
    QuickPreview = 0x0002,
    /// <summary>
    /// Content's preview imagery can be stored in the preview cache.
    /// </summary>
    PreviewCache = 0x0004,
    /// <summary>
    /// Content's preview imagery can be rendered progressively.
    /// </summary>
    ProgressivePreview = 0x0008,
    /// <summary>
    /// Texture UI includes an auto local mapping section for textures. See AddAutoParameters()
    /// </summary>
    LocalTextureMapping = 0x0010,
    /// <summary>
    /// Texture UI includes a graph section.
    /// </summary>
    GraphDisplay = 0x0020,
    /// <summary>
    /// Content supports UI sharing between contents of the same type id.
    /// </summary>
    SharedUI = 0x0040,
    /// <summary>
    /// Texture UI includes an adjustment section.
    /// </summary>
    Adjustment = 0x0080,
    /// <summary>
    /// Content uses fields to facilitate data storage and undo support. See Fields()
    /// </summary>
    Fields = 0x0100,
    /// <summary>
    /// Content supports editing in a modal editor.
    /// </summary>
    ModalEditing = 0x0200,
  }




  [AttributeUsage(AttributeTargets.Class)]
  public sealed class CustomRenderContentAttribute : System.Attribute
  {
    private RenderContentStyles m_style = RenderContentStyles.None;
    private readonly Guid m_renderengine_id;

    public CustomRenderContentAttribute()
    {
      m_renderengine_id = Guid.Empty;
    }
    public CustomRenderContentAttribute(String renderEngineGuid)
    {
      m_renderengine_id = new Guid(renderEngineGuid);
    }

    public Guid RenderEngineId
    {
      get { return m_renderengine_id; }
    }

    public RenderContentStyles Styles
    {
      get { return m_style; }
      set { m_style = value; }
    }

    public bool ImageBased { get; set; }
  }

  // Giulio thinks: this should be marked as [Flags]
  /// <summary>
  /// Defines constant values for all render content kinds, such as material, environment or texture.
  /// </summary>
  [Flags]
  enum RenderContentKind : int
  {
    None = 0,
    Material = 1,
    Environment = 2,
    Texture = 4,
  }

  public abstract class RenderContent : IDisposable
  {
    #region Kinds

    static RenderContentKind KindFromString(string kind)
    {
      RenderContentKind k = RenderContentKind.None;
      if (kind.Contains("material"))
        k |= RenderContentKind.Material;
      if (kind.Contains("environment"))
        k |= RenderContentKind.Environment;
      if (kind.Contains("texture"))
        k |= RenderContentKind.Texture;
      return k;
    }

    internal static String KindString(RenderContentKind kinds)
    {
      System.Text.StringBuilder sb = new System.Text.StringBuilder();

      if ((kinds & RenderContentKind.Material) == RenderContentKind.Material)
      {
        sb.Append("material");
      }

      if ((kinds & RenderContentKind.Environment) == RenderContentKind.Environment)
      {
        if (sb.Length != 0)
        {
          sb.Append(";");
        }
        sb.Append("environment");
      }

      if ((kinds & RenderContentKind.Texture) == RenderContentKind.Texture)
      {
        if (sb.Length != 0)
        {
          sb.Append(";");
        }
        sb.Append("texture");
      }
      return sb.ToString();
    }

    /// <summary>
    /// Internal string ids to be used in the GetString method.
    /// </summary>
    internal enum StringIds : int
    {
      Kind = 0,
      Name = 1,
      Notes = 2,
      TypeName = 3,
      TypeDescription = 4,
      ChildSlotName = 5,
      Xml = 6,

      //Material specific
      DiffuseChildSlotName = 100,
      TransparencyChildSlotName = 101,
      BumpChildSlotName = 102,
      EnvironmentChildSlotName = 103,
    }
    #endregion

    #region statics
    /// <summary>
    /// Render content is automatically registered for the Assembly that a plug-in is defined in. If
    /// you have content defined in a different assembly (for example a Grasshopper component), then
    /// you need to explicitly call RegisterContent.
    /// </summary>
    /// <param name="assembly">Assembly where custom content is defined.</param>
    /// <param name="pluginId">Parent plug-in for this assembly.</param>
    /// <returns>array of render content types registered on success. null on error.</returns>
    public static Type[] RegisterContent(System.Reflection.Assembly assembly, System.Guid pluginId)
    {
      Rhino.PlugIns.PlugIn plugin = Rhino.PlugIns.PlugIn.GetLoadedPlugIn(pluginId);
      if (plugin == null)
        return null;
      Type[] exported_types = assembly.GetExportedTypes();
      if (exported_types != null)
      {
        List<Type> content_types = new List<Type>();
        for (int i = 0; i < exported_types.Length; i++)
        {
          Type t = exported_types[i];
          if (!t.IsAbstract && t.IsSubclassOf(typeof(Rhino.Render.RenderContent)) && t.GetConstructor(new Type[] { }) != null)
            content_types.Add(t);
        }

        if (content_types.Count == 0)
          return null;

        // make sure that content types have not already been registered
        for (int i = 0; i < content_types.Count; i++)
        {
          if (RdkPlugIn.RenderContentTypeIsRegistered(content_types[i]))
            return null; //just bail
        }

        RdkPlugIn rdk_plugin = RdkPlugIn.GetRdkPlugIn(plugin);
        if (rdk_plugin == null)
          return null;

        rdk_plugin.AddRegisteredContentTypes(content_types);
        int count = content_types.Count;
        Guid[] ids = new Guid[count];
        for (int i = 0; i < count; i++)
        {
          ids[i] = content_types[i].GUID;
          if (content_types[i].IsSubclassOf(typeof(RenderTexture)))
            UnsafeNativeMethods.Rdk_AddTextureFactory(ids[i]);
          if (content_types[i].IsSubclassOf(typeof(RenderMaterial)))
            UnsafeNativeMethods.Rdk_AddMaterialFactory(ids[i]);
          if (content_types[i].IsSubclassOf(typeof(RenderEnvironment)))
            UnsafeNativeMethods.Rdk_AddEnvironmentFactory(ids[i]);
        }
        return content_types.ToArray();
      }
      return null;
    }

    internal static RenderContent FromInstanceId(Guid instanceId)
    {
      IntPtr renderContent = UnsafeNativeMethods.Rdk_FindContentInstance(instanceId);
      if (renderContent == IntPtr.Zero)
        return null;

      return FromPointer(renderContent);
    }

    internal static RenderContent FromPointer(IntPtr renderContent)
    {
      if (renderContent == IntPtr.Zero) return null;
      int serial_number = UnsafeNativeMethods.CRhCmnRenderContent_IsRhCmnDefined(renderContent);
      if (serial_number > 0)
        return FromSerialNumber(serial_number);

      IntPtr pTexture = UnsafeNativeMethods.Rdk_RenderContent_DynamicCastToTexture(renderContent);
      if (pTexture != IntPtr.Zero)
        return new NativeRenderTexture(pTexture);

      IntPtr pMaterial = UnsafeNativeMethods.Rdk_RenderContent_DynamicCastToMaterial(renderContent);
      if (pMaterial != IntPtr.Zero)
        return new NativeRenderMaterial(pMaterial);

      IntPtr pEnvironment = UnsafeNativeMethods.Rdk_RenderContent_DynamicCastToEnvironment(renderContent);
      if (pEnvironment != IntPtr.Zero)
        return new NativeRenderEnvironment(pEnvironment);

      //This should never, ever, happen.

      Debug.Assert(false);
      return null;
    }
    #endregion

    // -1 == Disposed content
    internal int m_runtime_serial_number;// = 0; initialized by runtime
    static int m_current_serial_number = 1;
    private int m_search_hint = -1;
    static readonly Dictionary<int, RenderContent> m_all_custom_content = new Dictionary<int, RenderContent>();

    // you never derive directly from RenderContent
    internal RenderContent(bool isCustomContent)
    {
      // This constructor is being called because we have a custom .NET subclass
      if (isCustomContent)
      {
        m_runtime_serial_number = m_current_serial_number++;
        m_all_custom_content.Add(m_runtime_serial_number, this);
      }
    }

    internal void Construct(Guid pluginId)
    {
      Type t = GetType();
      Guid render_engine = Guid.Empty;
      bool image_based = false;
      object[] attr = t.GetCustomAttributes(typeof(CustomRenderContentAttribute), false);
      if (attr != null && attr.Length > 0)
      {
        CustomRenderContentAttribute custom = attr[0] as CustomRenderContentAttribute;
        image_based = custom.ImageBased;
        render_engine = custom.RenderEngineId;
      }
      int category = 0;
      Guid type_id = t.GUID;

      if (this is RenderTexture)
      {
        UnsafeNativeMethods.CRhCmnTexture_New(m_runtime_serial_number, image_based, render_engine, pluginId, type_id, category);
      }
      else if (this is RenderMaterial)
      {
        UnsafeNativeMethods.CRhCmnMaterial_New(m_runtime_serial_number, image_based, render_engine, pluginId, type_id, category);
      }
      else if (this is RenderEnvironment)
      {
        UnsafeNativeMethods.CRhCmnEnvironment_New(m_runtime_serial_number, image_based, render_engine, pluginId, type_id, category);
      }
      else
      {
        Debug.Assert(false);
      }

      const BindingFlags flags = System.Reflection.BindingFlags.Public |
                                 System.Reflection.BindingFlags.NonPublic |
                                 System.Reflection.BindingFlags.Instance;
      System.Reflection.FieldInfo[] fields = t.GetFields(flags);
      if (fields != null)
      {
        for (int i = 0; i < fields.Length; i++)
        {
          if (fields[i].FieldType.IsSubclassOf(typeof(Field)))
          {
            Field f = fields[i].GetValue(this) as Field;
            if (f != null)
            {
              bool visibleInUi = m_autoui_fields.Contains(f);
              f.CreateCppPointer(this, visibleInUi);
            }
          }
        }
      }
    }

    internal string GetString(StringIds which)
    {
      IntPtr pConstThis = ConstPointer();
      using (Rhino.Runtime.StringHolder sh = new Rhino.Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        UnsafeNativeMethods.Rdk_RenderContent_GetString(pConstThis, pString, (int)which);
        return sh.ToString();
      }
    }

    /// <summary>
    /// Override this method to provide a name for your content type.  ie. "My .net Texture"
    /// </summary>
    public abstract String TypeName { get; }

    /// <summary>
    /// Override this method to provide a description for your content type.  ie.  "Procedural checker pattern"
    /// </summary>
    public abstract String TypeDescription { get; }

    // <summary>
    // Returns either KindMaterial, KindTexture or KindEnvironment.
    // </summary>
    //public RenderContentKind Kind
    //{
    //  get { return KindFromString(GetString(StringIds.Kind)); }
    //}

    /// <summary>
    /// Instance name for this content.
    /// </summary>
    public String Name
    {
      get { return GetString(StringIds.Name); }
      set
      {
        UnsafeNativeMethods.Rdk_RenderContent_SetInstanceName(ConstPointer(), value);
      }
    }

    /// <summary>
    /// Notes for this content.
    /// </summary>
    public String Notes
    {
      get { return GetString(StringIds.Notes); }
      set
      {
        UnsafeNativeMethods.Rdk_RenderContent_SetNotes(ConstPointer(), value);
      }
    }

    /// <summary>
    /// Instance identifier for this content.
    /// </summary>
    public Guid Id
    {
      get
      {
        return UnsafeNativeMethods.Rdk_RenderContent_InstanceId(ConstPointer());
      }
      set
      {
        UnsafeNativeMethods.Rdk_RenderContent_SetInstanceId(ConstPointer(), value);
      }
    }

    /// <summary>
    /// Returns true if this content has no parent, false if it is the child of another content.
    /// </summary>
    public bool TopLevel
    {
      get { return 1 == UnsafeNativeMethods.Rdk_RenderContent_IsTopLevel(ConstPointer()); }
    }

    // Hiding for the time being. It may be better to just have a Document property
    /// <summary>
    /// Returns true if this content is a resident of one of the persistant lists.
    /// </summary>
    /*public*/ bool InDocument
    {
      get { return 1 == UnsafeNativeMethods.Rdk_RenderContent_IsInDocument(ConstPointer()); }
    }
    /*
    /// <summary>
    /// Helper function to check which content kind this content is.
    /// </summary>
    /// <param name="kind">Either KindMaterial, KindEnvironment or KindTexture.</param>
    /// <returns>true if the content is the specified kind, otherwise false.</returns>
    public bool IsKind(RenderContentKind kind)
    {
      return 1 == UnsafeNativeMethods.Rdk_RenderContent_IsKind(ConstPointer(), KindString(kind));
    }
    */
    /// <summary>
    /// Determines if the content has the hidden flag set.
    /// </summary>
    public bool Hidden
    {
      get { return 1 == UnsafeNativeMethods.Rdk_RenderContent_IsHidden(ConstPointer()); }
      set { UnsafeNativeMethods.Rdk_RenderContent_SetIsHidden(NonConstPointer(), value); }
    }

    // hiding for the time being. If this is only for environments, it may make more sense
    // to place the property there
    /// <summary>
    /// Determines if the content is considered the "Current" content - currently only used for Environments.
    /// </summary>
    /*public*/ bool Current
    {
      get { return 1 == UnsafeNativeMethods.Rdk_RenderContent_IsCurrent(ConstPointer()); }
    }

    /// <summary>
    /// Returns the top content in this parent/child chain.
    /// </summary>
    public RenderContent TopLevelParent
    {
      get
      {
        IntPtr pContent = UnsafeNativeMethods.Rdk_RenderContent_TopLevelParent(ConstPointer());
        return FromPointer(pContent);
      }
    }

    #region Serialization

    // See if we can fit this into the standard .NET serialization method (ISerializable)
    /*
    public bool ReadFromXml(String inputXml)
    {
      return 1 == UnsafeNativeMethods.Rdk_RenderContent_ReadFromXml(NonConstPointer(), inputXml);
    }
    public String GetXml()
    {
      return GetString(StringIds.Xml);
    }
    */

    #endregion

    /// <summary>
    /// Override this function to provide UI sections to display in the editor.
    /// </summary>
    protected virtual void OnAddUserInterfaceSections()
    {
      if (IsNativeWrapper())
      {
        UnsafeNativeMethods.Rdk_AddUISections(NonConstPointer());
      }
      else
      {
        UnsafeNativeMethods.Rdk_CallAddUISectionsBase(NonConstPointer());
      }
    }

    // <summary>
    // If you want an automatic user interface to be constructed for a field,
    // call this function in your class constructor.
    // </summary>
    // <param name="f">A constructed field.</param>
    //protected void AddAutomaticUiField(Field f)
    //{
    //  m_autoui_fields.Add(f);
    //}

    protected void AddUserInterfaceField(string internalName, string friendlyName, string initialFieldValue)
    {
      m_autoui_fields.Add(new StringField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, bool initialFieldValue)
    {
      m_autoui_fields.Add(new BoolField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, int initialFieldValue)
    {
      m_autoui_fields.Add(new IntField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, double initialFieldValue)
    {
      m_autoui_fields.Add(new DoubleField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, Rhino.Display.Color4f initialFieldValue)
    {
      m_autoui_fields.Add(new ColorField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, Rhino.Geometry.Vector2d initialFieldValue)
    {
      m_autoui_fields.Add(new Vector2dField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, Rhino.Geometry.Vector3d initialFieldValue)
    {
      m_autoui_fields.Add(new Vector3dField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, Rhino.Geometry.Point2d initialFieldValue)
    {
      m_autoui_fields.Add(new Vector2dField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, Rhino.Geometry.Point3d initialFieldValue)
    {
      m_autoui_fields.Add(new Vector3dField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, Rhino.Geometry.Point4d initialFieldValue)
    {
      m_autoui_fields.Add(new Point4dField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, Guid initialFieldValue)
    {
      m_autoui_fields.Add(new GuidField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, Rhino.Geometry.Transform initialFieldValue)
    {
      m_autoui_fields.Add(new TransformField(internalName, friendlyName, initialFieldValue));
    }
    protected void AddUserInterfaceField(string internalName, string friendlyName, DateTime initialFieldValue)
    {
      m_autoui_fields.Add(new DateTimeField(internalName, friendlyName, initialFieldValue));
    }

    Field FindField(string name)
    {
      for (int i = 0; i < m_autoui_fields.Count; i++)
      {
        if( string.Compare(name, m_autoui_fields[i].InternalName, StringComparison.OrdinalIgnoreCase)==0 )
          return m_autoui_fields[i];
      }
      return null;
    }

    protected bool TryGetUserInterfaceField(string internalName, out string fieldValue)
    {
      fieldValue = String.Empty;
      StringField f = FindField(internalName) as StringField;
      if( f!=null )
        fieldValue = f.Value;
      return f!=null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out bool fieldValue)
    {
      fieldValue = false;
      BoolField f = FindField(internalName) as BoolField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out int fieldValue)
    {
      fieldValue = 0;
      IntField f = FindField(internalName) as IntField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out double fieldValue)
    {
      fieldValue = 0;
      DoubleField f = FindField(internalName) as DoubleField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out Rhino.Display.Color4f fieldValue)
    {
      fieldValue = Rhino.Display.Color4f.Empty;
      ColorField f = FindField(internalName) as ColorField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out Rhino.Geometry.Vector2d fieldValue)
    {
      fieldValue = Rhino.Geometry.Vector2d.Unset;
      var f = FindField(internalName) as Vector2dField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out Rhino.Geometry.Vector3d fieldValue)
    {
      fieldValue = Rhino.Geometry.Vector3d.Unset;
      var f = FindField(internalName) as Vector3dField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out Rhino.Geometry.Point2d fieldValue)
    {
      fieldValue = Rhino.Geometry.Point2d.Unset;
      var f = FindField(internalName) as Vector2dField;
      if (f != null)
        fieldValue = new Geometry.Point2d(f.Value);
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out Rhino.Geometry.Point3d fieldValue)
    {
      fieldValue = Rhino.Geometry.Point3d.Unset;
      var f = FindField(internalName) as Vector3dField;
      if (f != null)
        fieldValue = new Geometry.Point3d(f.Value);
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out Rhino.Geometry.Point4d fieldValue)
    {
      fieldValue = Rhino.Geometry.Point4d.Unset;
      var f = FindField(internalName) as Point4dField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out Guid fieldValue)
    {
      fieldValue = Guid.Empty;
      var f = FindField(internalName) as GuidField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out Rhino.Geometry.Transform fieldValue)
    {
      fieldValue = Rhino.Geometry.Transform.Identity;
      var f = FindField(internalName) as TransformField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }
    protected bool TryGetUserInterfaceField(string internalName, out DateTime fieldValue)
    {
      fieldValue = DateTime.Now;
      var f = FindField(internalName) as DateTimeField;
      if (f != null)
        fieldValue = f.Value;
      return f != null;
    }

    readonly List<Field> m_autoui_fields = new List<Field>();

    public bool AddUserInterfaceSection(string caption, int id)
    {
      return UnsafeNativeMethods.Rdk_CoreContent_AddAutomaticUISection(NonConstPointer(), caption, id);
    }


    // hiding until I understand what this does
    /*public virtual*/ bool IsContentTypeAcceptableAsChild(Guid type, String childSlotName)
    {
      if (IsNativeWrapper())
        return 1 == UnsafeNativeMethods.Rdk_RenderContent_IsContentTypeAcceptableAsChild(ConstPointer(), type, childSlotName);

      return 1 == UnsafeNativeMethods.Rdk_RenderContent_CallIsContentTypeAcceptableAsChildBase(ConstPointer(), type, childSlotName);
    }

    /*public*/ enum HarvestedResult : int // Return values for HarvestData()
    {
      None = 0,
      Some = 1,
      All = 2,
    };

    // hiding until I understand what this does
    /// <summary>
    /// Implement this to transfer data from another content to this content during creation.
    /// </summary>
    /// <param name="oldContent">An old content object from which the implementation may harvest data.</param>
    /// <returns>The harvested result.</returns>
    /*public virtual*/ HarvestedResult HarvestData(RenderContent oldContent)
    {
      if (IsNativeWrapper())
        return (HarvestedResult)UnsafeNativeMethods.Rdk_RenderContent_HarvestData(ConstPointer(), oldContent.ConstPointer());

      return (HarvestedResult)UnsafeNativeMethods.Rdk_RenderContent_CallHarvestDataBase(ConstPointer(), oldContent.ConstPointer());
    }
    
    #region Operations

    //TODO
    /** Delete a child content.
	\param parentContent is the content whose child is to be deleted. This must be an
	RDK-owned content that is in the persistent content list (either top-level or child).
	\param wszChildSlotName is the child-slot name of the child to be deleted.
	\return \e true if successful, else \e false. */
//RHRDK_SDK bool RhRdkDeleteChildContent(CRhRdkContent& parentContent, const wchar_t* wszChildSlotName);

    enum ChangeChildContentFlags : int
    {
      /// <summary>
      /// Allow (none) item to be displayed in dialog.
      /// </summary>
      AllowNone = 0x0001,
      /// <summary>
      /// Automatically open new content in thumbnail editor.
      /// </summary>
      AutoEdit = 0x0002,

      /// <summary>
      /// Mask to use to isolate harvesting flags.
      /// </summary>
      HarvestMask = 0xF000,
      /// <summary>
      /// Use Renderer Support option to decide about harvesting.
      /// </summary>
      HarvestUseOpt = 0x0000,
      /// <summary>
      /// Always copy similar parameters from old child.
      /// </summary>
      HarvestAlways = 0x1000,
      /// <summary>
      /// Never copy similar parameters from old child.
      /// </summary>
      HarvestNever = 0x2000,
    };
    //TODO
    /** Change a content's child by allowing the user to choose the new content type from a
      content browser dialog. The child is created if it does not exist, otherwise the old
      child is deleted and replaced by the new child.
      \param parentContent is the content whose child is to be manipulated. This must be an
      RDK-owned content that is in the persistent content list (either top-level or child).
      \param wszChildSlotName is the child-slot name of the child to be manipulated.
      \param allowedKinds determines which content kinds are allowed to be chosen from the content browser dialog.
      \param uFlags is a set of flags for controlling the content browser dialog.
      \return \e true if successful, \e false if it fails or if the user cancels. */

    //RHRDK_SDK bool RhRdkChangeChildContent(CRhRdkContent& parentContent, const wchar_t* wszChildSlotName,
    //                                      const CRhRdkContentKindList& allowedKinds,
    //                                     UINT uFlags = rdkccc_AllowNone | rdkccc_AutoEdit);
    #endregion

    internal enum ParameterTypes : int
    {
      Null = 0,
      Boolean = 1,
      Integer = 2,
      Float = 3,
      Double = 4,
      Color = 5,
      Vector2d = 6,
      Vector3d = 7,
      String = 8,
      Pointer = 9,
      Uuid = 10,
      Matrix = 11,
      Time = 12,
      Buffer = 13,
      Point4d = 14,
    }

    /// <summary>
    /// Context of a change to content parameters.
    /// </summary>
    public enum ChangeContexts
    {
      /// <summary>
      /// Change occurred as a result of user activity in the content's UI.
      /// </summary>
      UI = 0,
      /// <summary>
      /// Change occurred as a result of drag and drop.
      /// </summary>
      Drop = 1,
      /// <summary>
      /// Change occurred as a result of internal program activity.
      /// </summary>
      Program = 2,
      /// <summary>
      /// Change can be disregarded.
      /// </summary>
      Ignore = 3,
      /// <summary>
      /// Change occurred within the content tree (e.g., nodes reordered).
      /// </summary>
      Tree = 4,
      /// <summary>
      /// Change occurred as a result of an undo.
      /// </summary>
      Undo = 5,
      /// <summary>
      /// Change occurred as a result of a field initialization.
      /// </summary>
      FieldInit = 6,
      /// <summary>
      /// Change occurred during serialization (loading).
      /// </summary>
      Serialize = 7,
    }

    public void SetNamedParameter<T>(String parameterName, T value, ChangeContexts changeContext)
    {
      if (1 != UnsafeNativeMethods.Rdk_RenderContent_SetVariantParameter(ConstPointer(), parameterName, new Variant(value).ConstPointer(), (int)changeContext))
      {
        throw new InvalidOperationException("SetNamedParamter doesn't support this type.");
      }
    }

    /*public*/ object GetNamedParameter(String parameterName)
    {
      Rhino.Render.Variant variant = new Variant();
      if (IsNativeWrapper())
      {
        if (1==UnsafeNativeMethods.Rdk_RenderContent_GetVariantParameter(ConstPointer(), parameterName, variant.NonConstPointer()))
        {
          return variant;
        }
      }
      else
      {
        if (1 == UnsafeNativeMethods.Rdk_RenderContent_CallGetVariantParameterBase(ConstPointer(), parameterName, variant.NonConstPointer()))
        {
          return variant;
        }
      }
      throw new InvalidOperationException("Type not supported.");
    }

    /// <summary>
    /// See C++ RDK documentation - this is a passthrough function that gives access to your own
    /// native shader.  .NET clients will more likely simply check the type of their content and call their own
    /// shader access functions
    /// If you overide this function, you must ensure that you call "IsCompatible" and return IntPtr.Zero is that returns false.
    /// </summary>
    /// <param name="renderEngineId">The render engine requesting the shader.</param>
    /// <param name="privateData">A pointer to the render engine's own context object.</param>
    /// <returns>A pointer to the unmanaged shader.</returns>
    /*public virtual*/ IntPtr GetShader(Guid renderEngineId, IntPtr privateData)
    {
      if (IsNativeWrapper())
      {
        return UnsafeNativeMethods.Rdk_RenderContent_GetShader(ConstPointer(), renderEngineId, privateData);
      }
      return IntPtr.Zero;
    }

    /*public*/ bool IsCompatible(Guid renderEngineId)
    {
      return 1 == UnsafeNativeMethods.Rdk_RenderContent_IsCompatible(ConstPointer(), renderEngineId);
    }

    #region Child content support
    /// <summary>
    /// A "child slot" is the specific "slot" that a child (usually a texture) occupies.
    /// This is generally the "use" of the child - in other words, the thing the child
    /// operates on.  Some examples are "color", "transparency".
    /// </summary>
    /// <param name="paramName">The name of a parameter field. Since child textures will usually correspond with some
    ///parameter (they generally either replace or modify a parameter over UV space) these functions are used to
    ///specify which parameter corresponded with with child slot.  If there is no correspondance, return the empty
    ///string.</param>
    /// <returns>
    /// The default behaviour for these functions is to return the input string.
    /// Sub-classes may (in the future) override these functions to provide different mappings.
    /// </returns>
    public string ChildSlotNameFromParamName(String paramName)
    {
      using (Rhino.Runtime.StringHolder sh = new Rhino.Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        IntPtr pConstThis = ConstPointer();
        UnsafeNativeMethods.Rdk_RenderContent_ChildSlotNameFromParamName(pConstThis, paramName, pString);
        return sh.ToString();
      }
    }

    /// <summary>
    /// A "child slot" is the specific "slot" that a child (usually a texture) occupies.
    /// This is generally the "use" of the child - in other words, the thing the child
    /// operates on.  Some examples are "color", "transparency".
    /// </summary>
    /// <param name="childSlotName">The named of the child slot to receive the parameter name for.</param>
    /// <returns>The default behaviour for these functions is to return the input string.  Sub-classes may (in the future) override these functions to provide different mappings.</returns>
    public string ParamNameFromChildSlotName(String childSlotName)
    {
      using (Rhino.Runtime.StringHolder sh = new Rhino.Runtime.StringHolder())
      {
        IntPtr pString = sh.NonConstPointer();
        IntPtr pConstThis = ConstPointer();
        UnsafeNativeMethods.Rdk_RenderContent_ParamNameFromChildSlotName(pConstThis, childSlotName, pString);
        return sh.ToString();
      }
    }

    public RenderContent FindChild(String childSlotName)
    {
      IntPtr pConstThis = ConstPointer();
      IntPtr pChild = UnsafeNativeMethods.Rdk_RenderContent_FindChild(pConstThis, childSlotName);
      return RenderContent.FromPointer(pChild);
    }

    public String ChildSlotName
    {
      get { return GetString(StringIds.ChildSlotName); }
      set
      {
        UnsafeNativeMethods.Rdk_RenderContent_SetChildSlotName(ConstPointer(), value);
      }
    }

    #endregion

    #region C++->C# Callbacks

    internal delegate bool IsContentTypeAcceptableAsChildCallback(int serialNumber, Guid type, IntPtr childSlotName);
    internal static IsContentTypeAcceptableAsChildCallback m_IsContentTypeAcceptableAsChild = OnIsContentTypeAcceptableAsChild;
    static bool OnIsContentTypeAcceptableAsChild(int serialNumber, Guid type, IntPtr childSlotName)
    {
      try
      {
        RenderContent content = RenderContent.FromSerialNumber(serialNumber);
        if (content != null && childSlotName != IntPtr.Zero)
          return content.IsContentTypeAcceptableAsChild(type, System.Runtime.InteropServices.Marshal.PtrToStringUni(childSlotName));
      }
      catch (Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
      return false;
    }

    internal delegate int HarvestDataCallback(int serialNumber, IntPtr oldContent);
    internal static HarvestDataCallback m_HarvestData = OnHarvestData;
    static int OnHarvestData(int serialNumber, IntPtr oldContent)
    {
      try
      {
        RenderContent content = RenderContent.FromSerialNumber(serialNumber);
        RenderContent old = RenderContent.FromPointer(oldContent);
        if (content != null && old != null)
          return (int)content.HarvestData(old);
      }
      catch (Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
      return (int)HarvestedResult.None;
    }

    internal delegate void AddUISectionsCallback(int serialNumber);
    internal static AddUISectionsCallback m_AddUISections = _OnAddUISections;
    static void _OnAddUISections(int serialNumber)
    {
      try
      {
        RenderContent content = RenderContent.FromSerialNumber(serialNumber);
        if (content != null)
          content.OnAddUserInterfaceSections();
      }
      catch (Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
    }

    internal delegate void RenderContentDeleteThisCallback(int serialNumber);
    internal static RenderContentDeleteThisCallback m_DeleteThis = OnDeleteRhCmnRenderContent;
    static void OnDeleteRhCmnRenderContent(int serialNumber)
    {
      try
      {
        RenderContent content = RenderContent.FromSerialNumber(serialNumber);
        if (content != null)
        {
          content.m_runtime_serial_number = -1;
          m_all_custom_content.Remove(serialNumber);
        }
      }
      catch (Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
    }


    internal delegate void GetRenderContentStringCallback(int serialNumber, bool isName, IntPtr pON_wString);
    internal static GetRenderContentStringCallback m_GetRenderContentString = OnGetRenderContentString;
    static void OnGetRenderContentString(int serialNnumber, bool isName, IntPtr pON_wString)
    {
      try
      {
        RenderContent content = RenderContent.FromSerialNumber(serialNnumber);
        if (content != null)
        {
          string str = isName ? content.TypeName : content.TypeDescription;
          if (!string.IsNullOrEmpty(str))
            UnsafeNativeMethods.ON_wString_Set(pON_wString, str);
        }
      }
      catch (Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
    }

    internal delegate IntPtr GetShaderCallback(int serialNumber, Guid renderEngineId, IntPtr privateData);
    internal static GetShaderCallback m_GetShader = OnGetShader;
    static IntPtr OnGetShader(int serialNumber, Guid renderEngineId, IntPtr privateData)
    {
      try
      {
        RenderContent content = RenderContent.FromSerialNumber(serialNumber);
        if (content != null)
          return content.GetShader(renderEngineId, privateData);
      }
      catch (Exception ex)
      {
        Rhino.Runtime.HostUtils.ExceptionReport(ex);
      }
      return IntPtr.Zero;
    }

    #endregion



    #region events

    /*public*/ class ContentChangedEventArgs : RenderContentEventArgs
    {
      internal ContentChangedEventArgs(RenderContent content, RenderContent.ChangeContexts cc)
        : base(content)
      { m_cc = cc; }

      readonly RenderContent.ChangeContexts m_cc;
      public RenderContent.ChangeContexts ChangeContext { get { return m_cc; } }
    }

    /*public*/ class ContentTypeEventArgs : EventArgs
    {
      readonly Guid m_content_type;
      internal ContentTypeEventArgs(Guid type) { m_content_type = type; }
      public Guid Content { get { return m_content_type; } }
    }

    /*public*/ class ContentKindEventArgs : EventArgs
    {
      readonly RenderContentKind m_kind;
      internal ContentKindEventArgs(RenderContentKind kind) { m_kind = kind; }
      //public RenderContentKind Content { get { return m_kind; } }
    }

    /*public*/ class CurrentContentChangedEventArgs : RenderContentEventArgs
    {
      internal CurrentContentChangedEventArgs(RenderContent content, RenderContentKind kind)
        : base(content)
      { m_kind = kind; }

      readonly RenderContentKind m_kind;
      //public RenderContentKind Kind { get { return m_kind; } }
    }

    internal delegate void ContentAddedCallback(IntPtr pContent);
    internal delegate void ContentRenamedCallback(IntPtr pContent);
    internal delegate void ContentDeletingCallback(IntPtr pContent);
    internal delegate void ContentReplacingCallback(IntPtr pContent);
    internal delegate void ContentReplacedCallback(IntPtr pContent);
    internal delegate void ContentChangedCallback(IntPtr pContent, int changeContext);
    internal delegate void ContentUpdatePreviewCallback(IntPtr pContent);

    internal delegate void ContentTypeAddedCallback(Guid typeId);
    internal delegate void ContentTypeDeletingCallback(Guid typeId);
    internal delegate void ContentTypeDeletedCallback(int kind);

    internal delegate void CurrentContentChangedCallback(int kind, IntPtr pContent);

    private static CurrentContentChangedCallback m_OnCurrentContentChanged;
    private static void OnCurrentContentChanged(int kind, IntPtr pContent)
    {
      if (m_current_content_changed_event != null)
      {
        try { m_current_content_changed_event(null, new CurrentContentChangedEventArgs(Rhino.Render.RenderContent.FromPointer(pContent), (RenderContentKind)kind)); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<CurrentContentChangedEventArgs> m_current_content_changed_event;


    private static ContentTypeAddedCallback m_OnContentTypeAdded;
    private static void OnContentTypeAdded(Guid type)
    {
      if (m_content_type_added_event != null)
      {
        try { m_content_type_added_event(null, new ContentTypeEventArgs(type));  }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<ContentTypeEventArgs> m_content_type_added_event;

    private static ContentTypeDeletingCallback m_OnContentTypeDeleting;
    private static void OnContentTypeDeleting(Guid type)
    {
      if (m_content_type_deleting_event != null)
      {
        try { m_content_type_deleting_event(null, new ContentTypeEventArgs(type)); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<ContentTypeEventArgs> m_content_type_deleting_event;

    private static ContentTypeDeletedCallback m_OnContentTypeDeleted;
    private static void OnContentTypeDeleted(int kind)
    {
      if (m_content_type_deleted_event != null)
      {
        try { m_content_type_deleted_event(null, new ContentKindEventArgs((RenderContentKind)kind)); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<ContentKindEventArgs> m_content_type_deleted_event;
    


    private static ContentAddedCallback m_OnContentAdded;
    private static void OnContentAdded(IntPtr pContent)
    {
      if (m_content_added_event != null)
      {
        try
        {
          m_content_added_event(null, new RenderContentEventArgs(Rhino.Render.RenderContent.FromPointer(pContent)));
        }
        catch (Exception ex)
        {
          Runtime.HostUtils.ExceptionReport(ex);
        }
      }
    }
    internal static EventHandler<RenderContentEventArgs> m_content_added_event;

    private static ContentRenamedCallback m_OnContentRenamed;
    private static void OnContentRenamed(IntPtr pContent)
    {
      if (m_content_renamed_event != null)
      {
        try { m_content_renamed_event(null, new RenderContentEventArgs(Rhino.Render.RenderContent.FromPointer(pContent))); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<RenderContentEventArgs> m_content_renamed_event;

    private static ContentDeletingCallback m_OnContentDeleting;
    private static void OnContentDeleting(IntPtr pContent)
    {
      if (m_content_deleting_event != null)
      {
        try { m_content_deleting_event(null, new RenderContentEventArgs(Rhino.Render.RenderContent.FromPointer(pContent))); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<RenderContentEventArgs> m_content_deleting_event;

    private static ContentReplacingCallback m_OnContentReplacing;
    private static void OnContentReplacing(IntPtr pContent)
    {
      if (m_content_replacing_event != null)
      {
        try { m_content_replacing_event(null, new RenderContentEventArgs(Rhino.Render.RenderContent.FromPointer(pContent))); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<RenderContentEventArgs> m_content_replacing_event;

    private static ContentReplacedCallback m_OnContentReplaced;
    private static void OnContentReplaced(IntPtr pContent)
    {
      if (m_content_replaced_event != null)
      {
        try { m_content_replaced_event(null, new RenderContentEventArgs(Rhino.Render.RenderContent.FromPointer(pContent))); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<RenderContentEventArgs> m_content_replaced_event;

    private static ContentChangedCallback m_OnContentChanged;
    private static void OnContentChanged(IntPtr pContent, int cc)
    {
      if (m_content_changed_event != null)
      {
        try { m_content_changed_event(null, new ContentChangedEventArgs(Rhino.Render.RenderContent.FromPointer(pContent), (RenderContent.ChangeContexts)cc)); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<ContentChangedEventArgs> m_content_changed_event;

    private static ContentUpdatePreviewCallback m_OnContentUpdatePreview;
    private static void OnContentUpdatePreview(IntPtr pContent)
    {
      if (m_content_update_preview_event != null)
      {
        try { m_content_update_preview_event(null, new RenderContentEventArgs(Rhino.Render.RenderContent.FromPointer(pContent))); }
        catch (Exception ex) { Runtime.HostUtils.ExceptionReport(ex); }
      }
    }
    static EventHandler<RenderContentEventArgs> m_content_update_preview_event;
    
    /// <summary>
    /// Used to monitor render content addition to the document.
    /// </summary>
    public static event EventHandler<RenderContentEventArgs> RenderContentAdded
    {
      add
      {
        if (m_content_added_event == null)
        {
          m_OnContentAdded = OnContentAdded;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentAddedEventCallback(m_OnContentAdded, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_added_event += value;
      }
      remove
      {
        m_content_added_event -= value;
        if (m_content_added_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentAddedEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentAdded = null;
        }
      }
    }

    /// <summary>
    /// Used to monitor render content renaming in the document.
    /// </summary>
    /*public*/ static event EventHandler<RenderContentEventArgs> RenderContentRenamed
    {
      add
      {
        if (m_content_renamed_event == null)
        {
          m_OnContentRenamed = OnContentRenamed;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentRenamedEventCallback(m_OnContentRenamed, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_renamed_event += value;
      }
      remove
      {
        m_content_renamed_event -= value;
        if (m_content_renamed_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentRenamedEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentRenamed = null;
        }
      }
    }

    /// <summary>
    /// Used to monitor render content deletion from the document.
    /// </summary>
    /*public*/ static event EventHandler<RenderContentEventArgs> RenderContentDeleting
    {
      add
      {
        if (m_content_deleting_event == null)
        {
          m_OnContentDeleting = OnContentDeleting;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentDeletingEventCallback(m_OnContentDeleting, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_deleting_event += value;
      }
      remove
      {
        m_content_deleting_event -= value;
        if (m_content_deleting_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentDeletingEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentDeleting = null;
        }
      }
    }

    /// <summary>
    /// Used to monitor render content replacing in the document.
    /// </summary>
    /*public*/ static event EventHandler<RenderContentEventArgs> RenderContentReplacing
    {
      add
      {
        if (m_content_replacing_event == null)
        {
          m_OnContentReplacing = OnContentReplacing;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentReplacingEventCallback(m_OnContentReplacing, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_replacing_event += value;
      }
      remove
      {
        m_content_replacing_event -= value;
        if (m_content_replacing_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentReplacingEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentReplacing = null;
        }
      }
    }

    /// <summary>
    /// Used to monitor render content replacing in the document.
    /// </summary>
    /*public*/ static event EventHandler<RenderContentEventArgs> RenderContentReplaced
    {
      add
      {
        if (m_content_replaced_event == null)
        {
          m_OnContentReplaced = OnContentReplaced;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentReplacedEventCallback(m_OnContentReplaced, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_replaced_event += value;
      }
      remove
      {
        m_content_replaced_event -= value;
        if (m_content_replaced_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentReplacedEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentReplaced = null;
        }
      }
    }

    /// <summary>
    /// Used to monitor render content modifications.
    /// </summary>
    /*public*/ static event EventHandler<ContentChangedEventArgs> RenderContentChanged
    {
      add
      {
        if (m_content_changed_event == null)
        {
          m_OnContentChanged = OnContentChanged;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentChangedEventCallback(m_OnContentChanged, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_changed_event += value;
      }
      remove
      {
        m_content_changed_event -= value;
        if (m_content_changed_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentChangedEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentChanged = null;
        }
      }
    }

    /// <summary>
    /// Used to monitor render content preview updates.
    /// </summary>
    /*public*/ static event EventHandler<RenderContentEventArgs> RenderContentUpdatePreview
    {
      add
      {
        if (m_content_update_preview_event == null)
        {
          m_OnContentUpdatePreview = OnContentUpdatePreview;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentUpdatePreviewEventCallback(m_OnContentUpdatePreview, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_update_preview_event += value;
      }
      remove
      {
        m_content_update_preview_event -= value;
        if (m_content_update_preview_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentUpdatePreviewEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentUpdatePreview = null;
        }
      }
    }

    /// <summary>
    /// Used to monitor render content preview updates.
    /// </summary>
    /*public*/ static event EventHandler<CurrentContentChangedEventArgs> CurrentRenderContentChanged
    {
      add
      {
        if (m_current_content_changed_event == null)
        {
          m_OnCurrentContentChanged = OnCurrentContentChanged;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentCurrencyChangedEventCallback(m_OnCurrentContentChanged, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_current_content_changed_event += value;
      }
      remove
      {
        m_current_content_changed_event -= value;
        if (m_current_content_changed_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetContentCurrencyChangedEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnCurrentContentChanged = null;
        }
      }
    }



    /// <summary>
    /// Used to monitor render content types being registered.
    /// </summary>
    /*public*/ static event EventHandler<ContentTypeEventArgs> RenderContentTypeAdded
    {
      add
      {
        if (m_content_type_added_event == null)
        {
          m_OnContentTypeAdded = OnContentTypeAdded;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetFactoryAddedEventCallback(m_OnContentTypeAdded, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_type_added_event += value;
      }
      remove
      {
        m_content_type_added_event -= value;
        if (m_content_type_added_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetFactoryAddedEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentTypeAdded = null;
        }
      }
    }


    /// <summary>
    /// Used to monitor render content types being registered.
    /// </summary>
    /*public*/ static event EventHandler<ContentTypeEventArgs> RenderContentTypeDeleting
    {
      add
      {
        if (m_content_type_deleting_event == null)
        {
          m_OnContentTypeDeleting = OnContentTypeDeleting;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetFactoryDeletingEventCallback(m_OnContentTypeDeleting, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_type_deleting_event += value;
      }
      remove
      {
        m_content_type_deleting_event -= value;
        if (m_content_type_deleting_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetFactoryDeletingEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentTypeDeleting = null;
        }
      }
    }


    /// <summary>
    /// Used to monitor render content types being registered.
    /// </summary>
    /*public*/ static event EventHandler<ContentKindEventArgs> RenderContentTypeDeleted
    {
      add
      {
        if (m_content_type_deleted_event == null)
        {
          m_OnContentTypeDeleted = OnContentTypeDeleted;
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetFactoryDeletedEventCallback(m_OnContentTypeDeleted, Rhino.Runtime.HostUtils.m_rdk_ew_report);
        }
        m_content_type_deleted_event += value;
      }
      remove
      {
        m_content_type_deleted_event -= value;
        if (m_content_type_deleted_event == null)
        {
          UnsafeNativeMethods.CRdkCmnEventWatcher_SetFactoryDeletedEventCallback(null, Rhino.Runtime.HostUtils.m_rdk_ew_report);
          m_OnContentTypeDeleted = null;
        }
      }
    }


    #endregion

    #region pointer tracking

    private bool m_bAutoDelete;
    internal bool AutoDelete
    {
      get { return m_bAutoDelete; }
      set { m_bAutoDelete = value; }
    }

    internal static RenderContent FromSerialNumber(int serial_number)
    {
      RenderContent rc;
      m_all_custom_content.TryGetValue(serial_number, out rc);
      return rc;
    }

    internal virtual IntPtr ConstPointer()
    {
      IntPtr pContent = UnsafeNativeMethods.Rdk_FindRhCmnContentPointer(m_runtime_serial_number, ref m_search_hint);
      return pContent;
    }
    internal virtual IntPtr NonConstPointer()
    {
      IntPtr pContent = UnsafeNativeMethods.Rdk_FindRhCmnContentPointer(m_runtime_serial_number, ref m_search_hint);
      return pContent;
    }

    internal virtual bool IsNativeWrapper()
    {
      return false;
    }
    #endregion

    #region disposable implementation
    ~RenderContent()
    {
      Dispose(false);
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      if (m_bAutoDelete)
      {
        UnsafeNativeMethods.Rdk_RenderContent_DeleteThis(NonConstPointer());
      }
      // for now we, don't need to perform any disposal
      //if (IntPtr.Zero != m_pRenderContent)
      //{
      //  UnsafeNativeMethods.Rdk_RenderContent_DeleteThis(m_pRenderContent);
      //  m_pRenderContent = IntPtr.Zero;
      //}
    }
    #endregion
  }

  public class RenderContentEventArgs : EventArgs
  {
    readonly RenderContent m_content;
    internal RenderContentEventArgs(RenderContent content) { m_content = content; }
    public RenderContent Content { get { return m_content; } }
  }
}

#endif