﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using UnityEngine.UI;

public class InspectorFieldAttribute : Attribute
{
    public string FieldName { get { return m_fieldName; } }
    private string m_fieldName;

    public InspectorFieldAttribute( string fieldName )
    {
        this.m_fieldName = fieldName;
    }
}

public abstract class ComponentEditor : MonoBehaviour
{
    private Dictionary<string, InspectorField> m_Fields = new Dictionary<string, InspectorField>();

    private bool m_Active;
    [SerializeField] protected bool m_Fixed;

    private Dictionary<string, MethodInfo> m_Hooks = new Dictionary<string, MethodInfo>();
    private Toggle m_StateToggle;
    protected InspectorField GetField( string fieldName )
    {
        return m_Fields[ fieldName ];
    }

    public bool IsActive()
    {
        return m_Active;
    }

    protected virtual void Awake()
    {
        if (!m_Fixed)
        {
            m_StateToggle = GetComponentInChildren<Toggle>();
        }
        else
        {
            m_Active = true;
        }
        InitializeHooks();
        AddChangedHandlers();
    }

    private void SetToggleState( bool state )
    {
        m_StateToggle.onValueChanged.RemoveAllListeners();
        m_StateToggle.isOn = state;
        SubscriveStateToggleEvent();
    }

    private void SubscriveStateToggleEvent()
    {
        m_StateToggle.onValueChanged.AddListener( SetState );
    }

    protected void SetState( bool state )
    {
        if (m_Fixed) return;
        m_Active = state;

        SetToggleState( state );
        foreach (Transform field in base.transform)
        {
            if (field.tag != "Header")
            {
                field.gameObject.SetActive( state );
            }
        }
        if (state)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate( GameObject.Find( "Canvas" ).GetComponent<RectTransform>() );
            Canvas.ForceUpdateCanvases();
            foreach (var canvasGroup in GetComponentsInChildren<CanvasGroup>( true ))
            {
                /*Debug.Log( canvasGroup .name);
                var obj = canvasGroup.gameObject;
                Destroy( obj .GetComponent<GraphicRaycaster>());
                Destroy( obj .GetComponent<CanvasGroup>());
                Destroy( obj .GetComponent<Canvas>());
                obj.AddComponent<Canvas>();
                obj.AddComponent<GraphicRaycaster>();
                 obj.AddComponent<CanvasGroup>() ;*/
            }
        }
        if (InspectorView.SelectedItems.Count <= 1 || Input.GetKey( KeyCode.LeftControl ) &&
            Input.GetKey( KeyCode.LeftShift ))
        {
            OnStateChanged( state );
        }
    }

    protected abstract void OnStateChanged( bool state );

    protected virtual void InitializeHooks()
    {
        m_Hooks = GetInspectorFieldHandlers( GetType() );
    }

    private void AddChangedHandlers()
    {
        foreach (Transform child in transform)
        {
            var field = child.GetComponent<InspectorField>();
            if (field != null)
            {
                m_Fields.Add( field.Name, field );
                field.AddListener( obj => OnFieldChanged( field.Name, obj ) );
            }
        }
    }


    public abstract void OnItemsSelected( List<CUIObject> newItems );

    protected virtual void OnFieldChanged( string field, object value )
    {
        MethodInfo method;
        if (m_Hooks.TryGetValue( field, out method ))
        {
            method.Invoke( this, new object[] { value } );
        }
    }

    protected Dictionary<string, MethodInfo> GetInspectorFieldHandlers( Type type )
    {
        return type
            .GetMethods( BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance )
            .Where( m => m.GetCustomAttributes( typeof( InspectorFieldAttribute ), false ).Length > 0 )
            .ToDictionary(
                m => ( (InspectorFieldAttribute) m.GetCustomAttributes( typeof( InspectorFieldAttribute ), false )[ 0 ] ).FieldName,
                m => m );
    }

    protected Dictionary<string, MethodInfo> GetInspectorFieldHandlers<T>()
    {
        return GetInspectorFieldHandlers( typeof( T ) );
    }
}

public abstract class ComponentEditor<CT, CCT> : ComponentEditor
    where CCT : ICuiComponent
    where CT : BaseComponent<CCT>
{
    private Dictionary<string, MethodInfo> m_ComponentHooks = new Dictionary<string, MethodInfo>();

    private static ComponentEditor<CT, CCT> m_Instance;

    public static ComponentEditor<CT, CCT> Instance()
    {
        return m_Instance;
    }

    protected override void Awake()
    {
        base.Awake();
        m_Instance = this;
    }

    protected override void InitializeHooks()
    {
        base.InitializeHooks();
        m_ComponentHooks = GetInspectorFieldHandlers<CT>();
    }

    protected override void OnStateChanged( bool state )
    {
        if (state)
        {
            if (InspectorView.SelectedItems.Any( p => !p.CanBeAdd<CT, CCT>() ))
            {
                SetState( false );
                return;
            }
        }
        foreach (var selected in InspectorView.SelectedItems)
        {
            selected.OnComponentStateChanged( this, state );
        }

        if (state)
        {
            Load( InspectorView.SelectedItem.GetCuiComponent<CCT>() );
        }
    }

    protected override void OnFieldChanged( string field, object value )
    {
        MethodInfo method;
        if (m_ComponentHooks.TryGetValue( field, out method ))
        {
            foreach (var comp in InspectorView.GetSelectedComponents<CT>())
            {
                method.Invoke( comp, new object[] { value } );
            }
        }
        else
        {
            throw new Exception( "Field doesnt have handler in component" );
        }
        base.OnFieldChanged( field, value );
    }

    public abstract void Load( CCT component );

    public override void OnItemsSelected( List<CUIObject> newItems )
    {
        if (newItems == null || newItems.Count == 0 ||
            newItems.Any( cuiObject => cuiObject.GetCuiComponent<CCT>() == null ))
        {
            SetState( false );
            return;
        }
        SetState( true );
        Load( newItems.Last().GetCuiComponent<CCT>() );
    }

}
