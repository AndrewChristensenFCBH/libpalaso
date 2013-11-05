// --------------------------------------------------------------------------------------------
// <copyright from='2011' to='2011' company='SIL International'>
// 	Copyright (c) 2011, SIL International. All Rights Reserved.
//
// 	Distributable under the terms of either the Common Public License or the
// 	GNU Lesser General Public License, as specified in the LICENSING.txt file.
// </copyright>
// --------------------------------------------------------------------------------------------

#if __MonoCS__
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace X11.XKlavier
{
	/// <summary>
	/// Provides access to the xklavier XKB keyboarding engine methods.
	/// </summary>
	internal class XklEngine
	{
		private struct XklState
		{
			public int Group;
			public int Indicators;
		}

		private string[] m_GroupNames;

		public XklEngine()
		{
			var display = GetDisplayConnection();
			Engine = xkl_engine_get_instance(display);
		}

		public XklEngine(IntPtr display)
		{
			Engine = xkl_engine_get_instance(display);
		}

		/// <summary>
		/// Gets the X11 display connection that Mono already has open, rather than
		/// carefully opening and closing it on our own in a way that doesnt crash (FWNX-895).
		/// </summary>
		internal static IntPtr GetDisplayConnection()
		{
			// When running views tests that get to this code from C++ through libcom, using
			// reflection to load the SWF assembly has trouble finding it unless first access
			// SWF without reflection or load it from a more specific path.

			// DisplayHandle is a static field but won't be initialized until a XplatUIX11 is constructed.
			// Although a XplatUIX11 is already constructed when
			// running Flex, it is not already constructed when running unit tests. So make sure
			// it is constructed before requesting DisplayHandle so DisplayHandle is initialized.
			var swfAssembly = Assembly.GetAssembly(typeof(System.Windows.Forms.Form));
			var xplatuix11Type = swfAssembly.GetType("System.Windows.Forms.XplatUIX11");
			xplatuix11Type.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);

			var displayHandleField = xplatuix11Type.GetField("DisplayHandle", BindingFlags.Static | BindingFlags.NonPublic);
			var displayHandleValue = displayHandleField.GetValue(null);
			var displayConnection = (IntPtr)displayHandleValue;

			Debug.Assert(displayConnection != IntPtr.Zero, "Expected to have a handle on X11 display connection.");
			return displayConnection;
		}

		public void Close()
		{
		}

		internal IntPtr Engine { get; private set; }

		public string Name
		{
			get
			{
				var name = xkl_engine_get_backend_name(Engine);
				return Marshal.PtrToStringAuto(name);
			}
		}

		public int NumGroups
		{
			get { return xkl_engine_get_num_groups(Engine); }
		}

		public virtual string[] GroupNames
		{
			get
			{
				if (m_GroupNames == null)
				{
					int count = NumGroups;
					var names = xkl_engine_get_groups_names(Engine);
					var namePtrs = new IntPtr[count];
					Marshal.Copy(names, namePtrs, 0, count);
					m_GroupNames = new string[count];
					for (int i = 0; i < count; i++)
					{
						m_GroupNames[i] = Marshal.PtrToStringAuto(namePtrs[i]);
					}
				}
				return m_GroupNames;
			}
		}

		public int NextGroup
		{
			get { return xkl_engine_get_next_group(Engine); }
		}

		public int PrevGroup
		{
			get { return xkl_engine_get_prev_group(Engine); }
		}

		public int CurrentWindowGroup
		{
			get { return xkl_engine_get_current_window_group(Engine); }
		}

		public int DefaultGroup
		{
			get { return xkl_engine_get_default_group(Engine); }
			set { xkl_engine_set_default_group(Engine, value); }
		}

		public void SetGroup(int grp)
		{
			xkl_engine_lock_group(Engine, grp);
		}

		public void SetToplevelWindowGroup(bool fGlobal)
		{
			xkl_engine_set_group_per_toplevel_window(Engine, fGlobal);
		}

		public bool IsToplevelWindowGroup
		{
			get { return xkl_engine_is_group_per_toplevel_window(Engine); }
		}

		public int CurrentState
		{
			get
			{
				var statePtr = xkl_engine_get_current_state(Engine);
				var state = (XklState)Marshal.PtrToStructure(statePtr, typeof(XklState));
				return state.Group;
			}
		}

		public int CurrentWindowState
		{
			get
			{
				var window = xkl_engine_get_current_window(Engine);
				IntPtr statePtr;
				if (xkl_engine_get_state(Engine, window, out statePtr))
				{
					var state = (XklState)Marshal.PtrToStructure(statePtr, typeof(XklState));
					return state.Group;
				}
				return -1;
			}
		}

		public string LastError
		{
			get
			{
				var error = xkl_get_last_error();
				return Marshal.PtrToStringAuto(error);
			}
		}

		// from libXKlavier
		[DllImport("libxklavier")]
		private extern static IntPtr xkl_engine_get_instance(IntPtr display);

		[DllImport("libxklavier")]
		private extern static IntPtr xkl_engine_get_backend_name(IntPtr engine);

		[DllImport("libxklavier")]
		private extern static int xkl_engine_get_num_groups(IntPtr engine);

		[DllImport("libxklavier")]
		private extern static IntPtr xkl_engine_get_groups_names(IntPtr engine);

		[DllImport("libxklavier")]
		private extern static int xkl_engine_get_next_group(IntPtr engine);
		[DllImport("libxklavier")]
		private extern static int xkl_engine_get_prev_group(IntPtr engine);
		[DllImport("libxklavier")]
		private extern static int xkl_engine_get_current_window_group(IntPtr engine);
		[DllImport("libxklavier")]
		private extern static void xkl_engine_lock_group(IntPtr engine, int grp);
		[DllImport("libxklavier")]
		private extern static int xkl_engine_get_default_group(IntPtr engine);
		[DllImport("libxklavier")]
		private extern static void xkl_engine_set_default_group(IntPtr engine, int grp);
		[DllImport("libxklavier")]
		private extern static void xkl_engine_set_group_per_toplevel_window(IntPtr engine, bool isGlobal);
		[DllImport("libxklavier")]
		private extern static bool xkl_engine_is_group_per_toplevel_window(IntPtr engine);
		[DllImport("libxklavier")]
		private extern static IntPtr xkl_engine_get_current_state(IntPtr engine);
		[DllImport("libxklavier")]
		private extern static IntPtr xkl_engine_get_current_window(IntPtr engine);
		[DllImport("libxklavier")]
		private extern static bool xkl_engine_get_state(IntPtr engine, IntPtr win, out IntPtr state_out);
		[DllImport("libxklavier")]
		private extern static IntPtr xkl_get_last_error();
	}
}
#endif
