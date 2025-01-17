﻿/* Copyright (C) Itseez3D, Inc. - All Rights Reserved
* You may not use this file except in compliance with an authorized license
* Unauthorized copying of this file, via any medium is strictly prohibited
* Proprietary and confidential
* UNLESS REQUIRED BY APPLICABLE LAW OR AGREED BY ITSEEZ3D, INC. IN WRITING, SOFTWARE DISTRIBUTED UNDER THE LICENSE IS DISTRIBUTED ON AN "AS IS" BASIS, WITHOUT WARRANTIES OR
* CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED
* See the License for the specific language governing permissions and limitations under the License.
* Written by Itseez3D, Inc. <support@avatarsdk.com>, April 2017
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ItSeez3D.AvatarSdk.Core
{
	/// <summary>
	/// Utility class (singleton) that takes care of a few things.
	/// 1) Singleton instance is a MonoBehaviour added as a component to active GameObject in a scene.
	/// Having this we can spawn coroutines from methods of classes not derived from MonoBehaviour (such
	/// as Connection or Session).
	/// 2) AvatarSdkMgr instance holds implementations of abstract interfaces used across the plugin, such as
	/// IStringManager and IPersistentStorage.
	/// </summary>
	public class AvatarSdkMgr
	{
		public class AvatarSdkMgrComponent : MonoBehaviour
		{
			private void OnDisable()
			{
				AvatarSdkMgr.Instance.DisposeData();
			}
		}

		private static AvatarSdkMgr instance = null;
		private static object mutex = new object ();
		private static bool appIsQuitting = false;
		private static bool initialized = false;

		private IStringManager stringManager;
		private IPersistentStorage persistentStorage;
		private IFullbodyPersistentStorage fullbodyPersistentStorage;
		private GameObject utilityGameObject;
		private AvatarSdkSettings settings = new AvatarSdkSettings()
		{
			ForceRescaleLargeImages = true
		};

		private Dictionary<SdkType, IAvatarProvider> avatarProvidersContainer = new Dictionary<SdkType, IAvatarProvider>();

		private Dictionary<SdkType, IFullbodyAvatarProvider> fullbodyAvatarProvidersContainer = new Dictionary<SdkType, IFullbodyAvatarProvider>();

		private IoCContainer iocContainer = null;

		private SdkType sdkType;

		/// <summary>
		/// Should necessarily be called before any calls to any SDK classes are made.
		/// Parameters of this method allow you to override default implementations of abstract interfaces used
		/// across SDK.
		/// </summary>
		/// <param name="stringMgr">Instance of class that implements IStringManager.
		/// Will be used instead of DefaultStringManager. For example this is handy if you want to translate
		/// certain strings provided by SDK, such as progress and statuses. See IStringManager definition.</param>
		/// <param name="storage">Instance of class that implements IPersistentStorage. This is useful
		/// when your app stores data in locations different from default Application.persistentDataPath
		/// defined by Unity. The best way to implement this interface is to derive from DefaultPersistentStorage
		/// and override whatever methods you need (but this is up to app developers).</param>
		/// <param name="fullbodyStorage">Instance of class that implements IFullbodyPersistentStorage. 
		/// Storage for the fullbody avatars </param>
		public static void Init (
			IStringManager stringMgr = null,
			IPersistentStorage storage = null,
			IFullbodyPersistentStorage fullbodyStorage = null,
			SdkType sdkType = SdkType.Cloud
		)
		{
			if (initialized) {
				Debug.LogError ("SDK already initialized!");
				return;
			}

			initialized = true;
			var sdk = Instance;

			if (stringMgr == null)
				stringMgr = new DefaultStringManager();
			sdk.stringManager = stringMgr;

			if (storage == null)
				storage = new DefaultPersistentStorage();
			sdk.persistentStorage = storage;

			if (fullbodyStorage == null)
				fullbodyStorage = new FullbodyPersistentStorage();
			sdk.fullbodyPersistentStorage = fullbodyStorage;

			sdk.sdkType = sdkType;
			sdk.iocContainer = new IoCContainer(sdkType);
		}

		/// <summary>
		/// Gets a value indicating whether SDK mgr is initialized.
		/// </summary>
		public static bool IsInitialized { get { return initialized; } }

		/// <summary>
		/// Allows to switch between Cloud and Offline SDK.
		/// </summary>
		public static void ChangeSdkType(SdkType sdkType)
		{
			Instance.sdkType = sdkType;
			Instance.iocContainer.SetSdkType(sdkType);
		}

		/// <summary>
		/// Get current sdk type (Cloud or Offline)
		/// </summary>
		public static SdkType GetSdkType()
		{
			return Instance.sdkType;
		}

		/// <summary>
		/// Spawn coroutine outside of MonoBehaviour.
		/// </summary>
		public static void SpawnCoroutine (IEnumerator routine)
		{
#if UNITY_EDITOR
			Coroutines.EditorRunner.instance.Run(routine);
#else
            Instance.utilityGameObject.GetComponent<MonoBehaviour> ().StartCoroutine (routine);
#endif
		}

		/// <summary>
		/// Stop coroutine
		/// </summary>
		public static void StopCoroutine(string routineName)
		{
			Instance.utilityGameObject.GetComponent<MonoBehaviour>().StopCoroutine(routineName);
		}

		/// <summary>
		/// Return string modified (e.g. translated) by string manager.
		/// </summary>
		/// <param name="s">String to modify.</param>
		public static string Str (string s)
		{
			return Instance.stringManager.GetString (s);
		}

		/// <summary>
		/// Return IPeristentStorage implementation.
		/// </summary>
		public static IPersistentStorage Storage ()
		{
			return Instance.persistentStorage;
		}

		public static IFullbodyPersistentStorage FullbodyStorage()
		{
			return Instance.fullbodyPersistentStorage;
		}

		public static AvatarSdkSettings Settings
		{
			get
			{
				return Instance.settings;
			}
		}

		/// <summary>
		/// Return IoCContainer that allows to resolve interface implementations 
		/// </summary>
		public static IoCContainer IoCContainer
		{
			get { return Instance.iocContainer; }
		}

		/// <summary>
		/// Get an instance of the IAvatarProvider implementation for the current SDK type (Cloud or Offline)
		/// </summary>
		public static IAvatarProvider GetAvatarProvider()
		{
			if (!Instance.avatarProvidersContainer.ContainsKey(Instance.sdkType))
				Instance.avatarProvidersContainer.Add(Instance.sdkType, Instance.iocContainer.Create<IAvatarProvider>());
			return Instance.avatarProvidersContainer[Instance.sdkType];
		}

		/// <summary>
		/// Get an instance of the IAvatarProvider implementation for the current SDK type (Cloud or Offline)
		/// </summary>
		public static IFullbodyAvatarProvider GetFullbodyAvatarProvider()
		{
			if (!Instance.fullbodyAvatarProvidersContainer.ContainsKey(Instance.sdkType))
				Instance.fullbodyAvatarProvidersContainer.Add(Instance.sdkType, Instance.iocContainer.Create<IFullbodyAvatarProvider>());
			return Instance.fullbodyAvatarProvidersContainer[Instance.sdkType];
		}

		#region Singleton stuff

		public static AvatarSdkMgr Instance {
			get {
				if (!initialized) {
					Debug.LogError ("Cannot obtain Instance, SDK not initialized!");
					return null;
				}

				if (appIsQuitting) {
					Debug.LogWarning ("Instance already destroyed on application quit. Won't create again.");
					return null;
				}

				lock (mutex) {
					if (instance == null) {
						instance = new AvatarSdkMgr ();
						if (!Utils.IsDesignTime ()) {
							var sdkGameObject = new GameObject ("AvatarSdkMgr");
							sdkGameObject.AddComponent<AvatarSdkMgrComponent> ();  // add empty component
							instance.utilityGameObject = sdkGameObject;
							GameObject.DontDestroyOnLoad (sdkGameObject);
							Debug.LogFormat ("An instance of {0} is needed in the scene", typeof(AvatarSdkMgr));
							Debug.LogFormat ("{0} object was created with DontDestroyOnLoad", sdkGameObject);
						}
					}
					return instance;
				}
			}
		}

		public void DisposeData()
		{
			foreach (IAvatarProvider avatarProvider in avatarProvidersContainer.Values)
				avatarProvider.Dispose();

			foreach (IFullbodyAvatarProvider fullbodyAvatarProvider in fullbodyAvatarProvidersContainer.Values)
				fullbodyAvatarProvider.Dispose();
		}

		/// <summary>
		/// Courtesy to: http://answers.unity3d.com/questions/32128/how-to-access-startcoroutine-in-a-static-way.html
		/// When unity quits, it destroys objects in a random order.
		/// In principle, a Singleton is only destroyed when application quits.
		/// If any script calls Instance after it have been destroyed,
		/// it will create a buggy ghost object that will stay on the Editor scene
		/// even after stopping playing the Application. Really bad!
		/// So, this was made to be sure we're not creating that buggy ghost object.
		/// </summary>
		public void OnDestroy ()
		{
			appIsQuitting = true;
		}

		#endregion
	}
}
