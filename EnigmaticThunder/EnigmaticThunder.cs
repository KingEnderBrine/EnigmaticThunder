﻿using BepInEx;
using EnigmaticThunder.Modules;
using EnigmaticThunder.Util;
using EnigmaticThunder.Utils;
using RoR2;
using RoR2.ContentManagement;
using RoR2.Skills;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete
[module: UnverifiableCode]
#pragma warning disable 

namespace EnigmaticThunder
{
    [BepInPlugin(guid, modName, version)]
    public class EnigmaticThunderPlugin : BaseUnityPlugin
    {
        //be unique, though you're the same here.

        public const string guid = "com.EnigmaDev.EnigmaticThunder";
        public const string modName = "Enigmatic Thunder";
        public const string version = "0.1.0";

        public static EnigmaticThunderPlugin instance;

        /// <summary>
        /// Called BEFORE the first frame of the game.
        /// </summary>
        public static event Action awake;
        /// <summary>
        /// Called on the first frame of the game.
        /// </summary>
        internal static event EventHandler start;

        /// <summary>
        /// Called when the mod is disabled
        /// </summary>
        internal static event Action onDisable;
        /// <summary>
        /// Called on the mod's FixedUpdate
        /// </summary>
        internal static event Action onFixedUpdate;

        private static int vanillaErrors = 0;
        private static int modErrors = 0;

        private ContentPack internalContentPack = new ContentPack();
        
        /// <summary>
        /// Called before modules    the content pack.
        /// </summary>
        public event Action preContentPackLoad;
        /// <summary>
        /// Called AFTER the modules modify the content pack.
        /// </summary>
        public event Action postContentPackLoad;

        internal static ObservableCollection<Util.Module> modules = new ObservableCollection<Util.Module>(); 


        //@El Conserje call it ConserjeCore or I'll scream
        public EnigmaticThunderPlugin()
        {
            LogCore.logger = base.Logger;

            //Add listeners.
            BepInEx.Logging.Logger.Listeners.Add(new ErrorListener());
            BepInEx.Logging.Logger.Listeners.Add(new ChainLoaderListener());

            SingletonHelper.Assign<EnigmaticThunderPlugin>(ref EnigmaticThunderPlugin.instance, this);

            GatherModules();

            var networkCompatibilityHandler = new NetworkCompatibilityHandler();
            networkCompatibilityHandler.BuildModList();

            RoR2.RoR2Application.isModded = true;

            ErrorListener.vanillaErrors.addition += VanillaErrors_addition;
            ErrorListener.modErrors.addition += ModErrors_addition;

            ChainLoaderListener.OnChainLoaderFinished += OnChainLoaderFinished;

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
        }

        private void GatherModules() {
            List<Type> gatheredModules = Assembly.GetExecutingAssembly().GetTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(Util.Module))).ToList();
            foreach (Type module in gatheredModules) {
                //Create instance.
                Util.Module item = (Util.Module)Activator.CreateInstance(module);
                //Log
                LogCore.LogI("Enabling module: " + item);
                //Fire
                item.Load();
                //Add to collection
                modules.Add(item);

            }

        }

        private void OnChainLoaderFinished()
        {
            //Wait until all mods have loaded.
            RoR2.ContentManagement.ContentManager.collectContentPackProviders += ContentManager_collectContentPackProviders;
        }

        private void ContentManager_collectContentPackProviders(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
        {
            addContentPackProvider(new EnigmaticContent());
        }

        private void ModErrors_addition(ErrorListener.LogMessage objectRemoved)
        {
            modErrors++;
        }

        private void VanillaErrors_addition(ErrorListener.LogMessage msg)
        {
            vanillaErrors++;
        }


        private void SceneManager_activeSceneChanged(Scene arg0, Scene arg1)
        {
            if (arg1.name == "title")
            {
                var menu = GameObject.Find("MainMenu");
                //LogCore.LogI(menu.name)
                var title = menu.transform.Find("MENU: Title/TitleMenu/SafeZone/ImagePanel (JUICED)/LogoImage");
                var indicator = menu.transform.Find("MENU: Title/TitleMenu/MiscInfo/Copyright/Copyright (1)");

                var build = indicator.GetComponent<HGTextMeshProUGUI>();

                build.fontSize += 6;
                build.text = build.text + Environment.NewLine + $"EnigmaticThunder Version: {version}";
                //build.text = build.text + Environment.NewLine + $"R2API Version: { R2API.R2API.PluginVersion }";
                build.text = build.text + Environment.NewLine + $"Vanilla Errors: {vanillaErrors.ToString()}";
                build.text = build.text + Environment.NewLine + $"Mod Errors: {modErrors.ToString()}";
            }
        }
  
        #region Events
        public void Awake()
        {
            FixEntityStates.RegisterStates();
            Action awake = EnigmaticThunderPlugin.awake;
            if (awake == null)
            {
                return;
            }
            awake();
        }

        public void FixedUpdate()
        {


            Action fixedUpdate = EnigmaticThunderPlugin.onFixedUpdate;
            if (fixedUpdate == null)
            {
                return;
            }
            fixedUpdate();
        }

        public void Start()
        {
            start?.Invoke(this, null);
        }

        public void OnDisable()
        {
            SingletonHelper.Unassign<EnigmaticThunderPlugin>(EnigmaticThunderPlugin.instance, this);
            Action awake = EnigmaticThunderPlugin.onDisable;
            if (awake == null)
            {
                return;
            }
            awake();
        }
        #endregion
    }
}