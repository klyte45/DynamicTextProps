using ColossalFramework.Globalization;
using ColossalFramework.UI;
using Klyte.Commons.Extensors;
using Klyte.Commons.Interfaces;
using Klyte.Commons.Utils;
using Klyte.DynamicTextProps.UI;
using Klyte.DynamicTextProps.UI.Images;
using Klyte.DynamicTextProps.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

[assembly: AssemblyVersion("1.99.999.*")]
namespace Klyte.DynamicTextProps
{
    public class DynamicTextPropsMod : BasicIUserMod<DynamicTextPropsMod, DTPResourceLoader, DTPController, DTPPanel, CommonSpriteNames>
    {
        public DynamicTextPropsMod() => Construct();

        public override string SimpleName => "Klyte's Dynamic Text Props";

        public override string Description => "This mod allows creating dynamic text props in the city";

        public override List<Tuple<string, Type, int, int>> SpriteSheets { get; } = new List<Tuple<string, Type, int, int>>()
        {
            Tuple.New( "UI.Images.sprites.png",typeof(CommonSpriteNames),64,64),
            Tuple.New( "UI.Images.lineFormats.png",typeof(LineIconSpriteNames),64,64),
        };

        public override void DoErrorLog(string fmt, params object[] args) => LogUtils.DoErrorLog(fmt, args);

        public override void DoLog(string fmt, params object[] args) => LogUtils.DoLog(fmt, args);

        public override void LoadSettings()
        {
        }

        public override void OnReleased() => base.OnReleased();

        public override void TopSettingsUI(UIHelperExtension helper)
        {

            UIHelperExtension group8 = helper.AddGroupExtended(Locale.Get("K45_DTP_GENERAL_INFO"));
            AddFolderButton(DefaultBuildingsConfigurationFolder, group8, "K45_DTP_DEFAULT_BUILDINGS_CONFIG_PATH_TITLE");
            group8.AddLabel(Locale.Get("K45_DTP_GET_FILES_GITHUB"));
            group8.AddButton(Locale.Get("K45_DTP_GO_TO_GITHUB"), () => Application.OpenURL("https://github.com/klyte45/DynamicTextPropsFiles"));
        }
        protected void BuildSurfaceFont(out UIDynamicFont font, string fontName)
        {
            font = ScriptableObject.CreateInstance<UIDynamicFont>();

            var fontList = new List<string> { fontName };
            font.baseFont = Font.CreateDynamicFontFromOSFont(fontList.ToArray(), 64);
            font.lineHeight = 70;
            font.baseline = 66;
            font.size = 64;
        }

        private static void AddFolderButton(string filePath, UIHelperExtension helper, string localeId)
        {
            FileInfo fileInfo = FileUtils.EnsureFolderCreation(filePath);
            helper.AddLabel(Locale.Get(localeId) + ":");
            var namesFilesButton = ((UIButton) helper.AddButton("/", () => ColossalFramework.Utils.OpenInFileBrowser(fileInfo.FullName)));
            namesFilesButton.textColor = Color.yellow;
            KlyteMonoUtils.LimitWidth(namesFilesButton, 710);
            namesFilesButton.text = fileInfo.FullName + Path.DirectorySeparatorChar;
        }


        public static readonly string FOLDER_NAME = FileUtils.BASE_FOLDER_PATH + "DynamicTextProps";
        internal static readonly string m_defaultFileNameXml = "DefaultBuildingsConfig";
        private UILabel refBut;
        public const string DEFAULT_GAME_BUILDINGS_CONFIG_FOLDER = "BuildingsDefaultPlacing";
        public const string DEFAULT_GAME_VEHICLES_CONFIG_FOLDER = "VehiclesDefaultPlacing";

        public static string DefaultBuildingsConfigurationFolder { get; } = FOLDER_NAME + Path.DirectorySeparatorChar + DEFAULT_GAME_BUILDINGS_CONFIG_FOLDER;
        public static string DefaultVehiclesConfigurationFolder { get; } = FOLDER_NAME + Path.DirectorySeparatorChar + DEFAULT_GAME_VEHICLES_CONFIG_FOLDER;

    }
}
