#include "StdAfx.h"

typedef void (CALLBACK* USERDATATRANSFORMPROC)(int serial_number, const ON_Xform* xform);
typedef int (CALLBACK* USERDATAARCHIVEPROC)(int serial_number);
typedef int (CALLBACK* USERDATAIOPROC)(int serial_number, int writing, ON_BinaryArchive* binary_archive);
typedef int (CALLBACK* USERDATADUPLICATEPROC)(int serial_number, ON_UserData* pUserData);
typedef ON_UserData* (CALLBACK* USERDATACREATEPROC)(ON_UUID id);
typedef void (CALLBACK* USERDATADELETEPROC)(int serial_number);

class CRhCmnUserData : public ON_UserData
{
public:
  static USERDATATRANSFORMPROC m_transform;
  static USERDATAARCHIVEPROC m_archive;
  static USERDATAIOPROC m_readwrite;
  static USERDATADUPLICATEPROC m_duplicate;
  static USERDATACREATEPROC m_create;
  static USERDATADELETEPROC m_delete;
public:
  CRhCmnUserData(int serial_number, ON_UUID managed_type_id, ON_UUID plugin_id, const wchar_t* description);
  virtual ~CRhCmnUserData();
  
  ON_UUID ManagedTypeId() const { return m_userdata_uuid; }
  ON_UUID PlugInId() const { return m_application_uuid; }

  virtual BOOL GetDescription( ON_wString& description );
  virtual BOOL Transform(const ON_Xform& xform);
  virtual BOOL Archive() const; 
  virtual BOOL Write( ON_BinaryArchive& binary_archive ) const;
  virtual BOOL Read( ON_BinaryArchive& binary_archive );

  int m_serial_number;
public:
  virtual const ON_ClassId* ClassId() const;
  
private:
  //record used for ON_Object runtime type information
  ON_ClassId* m_pClassId;
  //used by Duplicate to create copy of an object.
  virtual ON_Object* DuplicateObject() const;
  ON_wString m_description;
};

USERDATATRANSFORMPROC CRhCmnUserData::m_transform = NULL;
USERDATAARCHIVEPROC CRhCmnUserData::m_archive = NULL;
USERDATAIOPROC CRhCmnUserData::m_readwrite = NULL;
USERDATADUPLICATEPROC CRhCmnUserData::m_duplicate = NULL;
USERDATACREATEPROC CRhCmnUserData::m_create = NULL;
USERDATADELETEPROC CRhCmnUserData::m_delete = NULL;

CRhCmnUserData::CRhCmnUserData(int serial_number, ON_UUID managed_type_id, ON_UUID plugin_id, const wchar_t* description)
{
  m_description = description;
  m_serial_number = serial_number;
  m_userdata_uuid = managed_type_id;
  m_application_uuid = plugin_id;
  m_pClassId = NULL;
  // Dale Lear mentioned that in almost all cases, users want to have their
  // user data copied around. Set the default to be able to do this.
  m_userdata_copycount = 1;
}



CRhCmnUserData::~CRhCmnUserData()
{
  // Tell .NET that this class is being deleted
  // call a function passing m_serial_number
  if( m_delete )
    m_delete(m_serial_number);
}

BOOL CRhCmnUserData::GetDescription( ON_wString& description )
{
  description = m_description;
  return TRUE;
}

BOOL CRhCmnUserData::Transform(const ON_Xform& xform)
{
  // Tell .NET that Transform virtual function has been called
  if( m_transform )
    m_transform(m_serial_number, &xform);
  return TRUE;
}

BOOL CRhCmnUserData::Archive() const
{
  if( NULL==m_archive || NULL==m_readwrite )
    return FALSE;
  // Ask .Net if this should be archived
  int rc = m_archive(m_serial_number);
  return rc>0?TRUE:FALSE;
}

BOOL CRhCmnUserData::Write( ON_BinaryArchive& binary_archive ) const
{
  BOOL rc = FALSE;
  if( m_readwrite )
    rc = m_readwrite(m_serial_number, 1, &binary_archive);
  return rc;
}

BOOL CRhCmnUserData::Read( ON_BinaryArchive& binary_archive )
{
  BOOL rc = FALSE;
  if( m_readwrite )
    rc = m_readwrite(m_serial_number, 0, &binary_archive);
  return rc;
}


class CRhCmnClassId : public ON_ClassId
{
public:
  CRhCmnClassId( ON_UUID mgd_object_type,
                 const char* class_name,
                 const char* baseclass_name,
                 const char* sUUID );

  ON_UUID m_managed_object_type;
};


class CRhCmnClassIdList
{
public:
  CRhCmnClassIdList(){}
  ~CRhCmnClassIdList();

  const CRhCmnClassId* GetClassId( const ON_UUID& id );

  ON_SimpleArray<CRhCmnClassId*> m_class_ids;
};

CRhCmnClassIdList::~CRhCmnClassIdList()
{
  int count = m_class_ids.Count();
  for( int i=0; i<count; i++ )
  {
    CRhCmnClassId* pClassId = m_class_ids[i];
    if( pClassId )
      delete pClassId;
  }
}

const CRhCmnClassId* CRhCmnClassIdList::GetClassId( const ON_UUID& id )
{
  int count = m_class_ids.Count();
  for( int i=0; i<count; i++ )
  {
    const CRhCmnClassId* pClassId = m_class_ids[i];
    if( pClassId && pClassId->Uuid() == id )
      return pClassId;
  }
  return NULL;
}

static CRhCmnClassIdList g_classIds;

static ON_Object* RhCmnClassIdCreateOnObject()
{
  ON_UUID managed_type_id = ON_GetMostRecentClassIdCreateUuid();
  const CRhCmnClassId* pClassId = g_classIds.GetClassId(managed_type_id);
  if( !pClassId || NULL==CRhCmnUserData::m_create)
    return NULL;
  
  ON_UserData* rc = CRhCmnUserData::m_create(pClassId->m_managed_object_type);
  return rc;
}

static bool CopyRhCmnUserData( const ON_Object* src, ON_Object* dst )
{
  CRhCmnUserData* d = dynamic_cast<CRhCmnUserData*>(dst);
  const CRhCmnUserData* s = dynamic_cast<const CRhCmnUserData*>(src);
  if( !d || !s )
    return false;

  if( d->PlugInId() != s->PlugInId() )
    return false;
  if( d->ManagedTypeId() != s->ManagedTypeId() )
    return false;

  if( d->m_serial_number<1 && CRhCmnUserData::m_duplicate )
  {
    int serial_number = CRhCmnUserData::m_duplicate(s->m_serial_number, d);
    d->m_serial_number = serial_number;
  }
  return true;
}

CRhCmnClassId::CRhCmnClassId( ON_UUID mgd_object_type,
                              const char* class_name,
                              const char* baseclass_name,
                              const char* sUUID )
: ON_ClassId( class_name, baseclass_name, RhCmnClassIdCreateOnObject, CopyRhCmnUserData, sUUID )
{
  m_managed_object_type = mgd_object_type;
}

const ON_ClassId* CRhCmnUserData::ClassId() const
{
  if( !m_pClassId )
  {
    int count = g_classIds.m_class_ids.Count();
    for( int i=0; i<count; i++ )
    {
      ON_ClassId* pCid = g_classIds.m_class_ids[i];
      if( pCid && (pCid->Uuid() == m_userdata_uuid ) )
      {
        CRhCmnUserData* pThis = const_cast<CRhCmnUserData*>(this);
        pThis->m_pClassId = pCid;
        break;
      }
    }

    if( !m_pClassId && ON_UuidIsNotNil(m_userdata_uuid) )
    {
      // This should never happen.
      CRhCmnUserData* pThis = const_cast<CRhCmnUserData*>(this);

      ON_UUID managed_type_id = this->ManagedTypeId();
      ON_String sUUID;
      ::ON_UuidToString(managed_type_id, sUUID);
      CRhCmnClassId* pNewClassId = new CRhCmnClassId(managed_type_id,"CRhCmnUserData","ON_UserData",sUUID.Array());
      pThis->m_pClassId = pNewClassId;
      g_classIds.m_class_ids.Append( pNewClassId );
    }
    if( !m_pClassId )
      return __super::ClassId();
  }
  return m_pClassId;
}

ON_Object* CRhCmnUserData::DuplicateObject() const
{
  if( NULL==m_duplicate )
    return NULL;

  ON_UUID managed_type_id = ManagedTypeId();
  ON_UUID plugin_id = PlugInId();
  CRhCmnUserData* pNative = new CRhCmnUserData(-1, managed_type_id, plugin_id, m_description);
  int serial_number = m_duplicate(m_serial_number, pNative);
  if( serial_number<1 )
  {
    delete pNative;
    return NULL;
  }
  pNative->m_serial_number = serial_number;
  return pNative;
}

RH_C_FUNCTION void ON_UserData_RegisterCustomUserData( const RHMONO_STRING* managed_type_name, ON_UUID managed_type_id )
{
  INPUTSTRINGCOERCE(_name, managed_type_name);
  ON_String class_name(_name);

  // make sure this class is not already registered
  if( class_name.Length()<1 || g_classIds.GetClassId( managed_type_id ) != NULL )
    return;

  ON_String sUUID;
  ::ON_UuidToString(managed_type_id, sUUID);

  CRhCmnClassId* pClassId = new CRhCmnClassId(managed_type_id, class_name.Array(), "ON_UserData", sUUID.Array());
  g_classIds.m_class_ids.Append(pClassId);
}

RH_C_FUNCTION CRhCmnUserData* CRhCmnUserData_New( int serial_number, ON_UUID managed_type_id, ON_UUID plugin_id, const RHMONO_STRING* description)
{
  INPUTSTRINGCOERCE(_description, description);
  CRhCmnUserData* rc = new CRhCmnUserData(serial_number, managed_type_id, plugin_id, _description);
  return rc;
}

RH_C_FUNCTION void CRhCmnUserData_Delete(CRhCmnUserData* pUserData, int serial_number)
{
  if( pUserData )
    delete pUserData;
}

RH_C_FUNCTION int CRhCmnUserData_Find(const ON_Object* pConstOnObject, ON_UUID managed_type_id)
{
  int rc = -1;
  if( pConstOnObject )
  {
    ON_UserData* pUD = pConstOnObject->GetUserData(managed_type_id);
    CRhCmnUserData* pRhCmnUd = dynamic_cast<CRhCmnUserData*>(pUD);
    if( pRhCmnUd )
      rc = pRhCmnUd->m_serial_number;
  }
  return rc;
}

RH_C_FUNCTION void CRhCmnUserData_SetCallbacks(USERDATATRANSFORMPROC xform_proc,
                                               USERDATAARCHIVEPROC archive_proc,
                                               USERDATAIOPROC io_proc,
                                               USERDATADUPLICATEPROC duplicate_proc,
                                               USERDATACREATEPROC create_proc,
                                               USERDATADELETEPROC delete_proc)
{
  CRhCmnUserData::m_transform = xform_proc;
  CRhCmnUserData::m_archive = archive_proc;
  CRhCmnUserData::m_readwrite = io_proc;
  CRhCmnUserData::m_duplicate = duplicate_proc;
  CRhCmnUserData::m_create = create_proc;
  CRhCmnUserData::m_delete = delete_proc;
}

///////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////

struct CUserDataHolderPiece
{
  ON_UserDataHolder* m_pHolder;
  ON_UUID m_id;
};

static ON_SimpleArray<CUserDataHolderPiece> m_all_holders;

RH_C_FUNCTION bool ON_UserDataHolder_MoveUserDataFrom( ON_UUID id, const ON_Object* pConstObject)
{
  bool rc = false;
  if( ON_UuidIsNotNil(id) && pConstObject && pConstObject->FirstUserData()!=NULL )
  {
    //make sure the id is not already in the list. Note this list should almost
    //always have around 1 element, so linear search is fine
    for( int i=0; i<m_all_holders.Count(); i++ )
    {
      if( m_all_holders[i].m_id == id )
        return false;
    }

    ON_UserDataHolder* pHolder = new ON_UserDataHolder();
    rc = pHolder->MoveUserDataFrom(*pConstObject);
    if( !rc )
    {
      delete pHolder;
      return false;
    }

    CUserDataHolderPiece& piece = m_all_holders.AppendNew();
    piece.m_id = id;
    piece.m_pHolder = pHolder;
    rc = true;
  }
  return rc;
}

RH_C_FUNCTION void ON_UserDataHolder_MoveUserDataTo( ON_UUID id, const ON_Object* pConstObject, bool append)
{
  if( ON_UuidIsNotNil(id) && pConstObject )
  {
    for( int i=0; i<m_all_holders.Count(); i++ )
    {
      if( m_all_holders[i].m_id == id )
      {
        ON_UserDataHolder* pHolder = m_all_holders[i].m_pHolder;
        m_all_holders.Remove(i);
        if( pHolder )
        {
          pHolder->MoveUserDataTo(*pConstObject, append);
        }
      }
    }
  }
}
