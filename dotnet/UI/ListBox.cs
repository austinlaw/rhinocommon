﻿#pragma warning disable 1591
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Rhino.UI
{
  sealed partial class ListBoxForm : Form
  {
    public ListBoxForm(string title, string message, System.Collections.IList items)
    {
      InitializeComponent();
      m_checkedListBox.Visible = false;

      if (!string.IsNullOrEmpty(title))
        Text = title;
      if (!string.IsNullOrEmpty(message))
        m_lblMessage.Text = message;

      if (items != null)
      {
        object[] list = new object[items.Count];
        items.CopyTo(list, 0);
        m_list.Items.AddRange(list);
      }
    }

    public ListBoxForm(string title, string message, System.Collections.IList items, IList<bool> itemState)
    {
      InitializeComponent();
      m_list.Visible = false;
      m_checkedListBox.Visible = true;

      if (!string.IsNullOrEmpty(title))
        Text = title;
      if (!string.IsNullOrEmpty(message))
        m_lblMessage.Text = message;
      if (items != null)
      {
        object[] list = new object[items.Count];
        items.CopyTo(list, 0);
        m_checkedListBox.Items.AddRange(list);
        if (itemState != null && itemState.Count == items.Count)
        {
          for (int i = 0; i < items.Count; i++)
          {
            m_checkedListBox.SetItemChecked(i, itemState[i]);
          }
        }
      }
    }

    public object SelectedItem()
    {
      return m_list.SelectedItem;
    }

    public bool[] GetCheckedItemStates()
    {
      bool[] rc = null;
      int count = m_checkedListBox.Items.Count;
      if (count > 0)
      {
        rc = new bool[count];
        for (int i = 0; i < count; i++)
        {
          rc[i] = m_checkedListBox.GetItemChecked(i);
        }
      }
      return rc;
    }
  }
}
