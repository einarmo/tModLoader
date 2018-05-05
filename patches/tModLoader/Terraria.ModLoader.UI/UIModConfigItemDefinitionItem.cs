﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader.UI.Elements;
using Terraria.UI;

namespace Terraria.ModLoader.UI
{
	// JSONItemConverter should allow this to be used as a dictionary key.
	[TypeConverter(typeof(JSONItemConverter))]
	//[CustomModConfigItem(typeof(UIModConfigItemDefinitionItem))]
	public class JSONItem
	{
		public string mod;
		public string name;

		public JSONItem()
		{
			mod = "";
			name = "";
		}

		public JSONItem(string mod, string name)
		{
			this.mod = mod;
			this.name = name;
		}

		public bool IsUnloaded => GetID() == 0 && !(name == "" && mod == "");

		public override bool Equals(object obj)
		{
			JSONItem p = obj as JSONItem;
			if (p == null)
			{
				return false;
			}
			return (mod == p.mod) && (name == p.name);
		}

		public override int GetHashCode()
		{
			return new { mod, name }.GetHashCode();
		}

		public int GetID()
		{
			if (mod == "Terraria")
			{
				if (!ItemID.Search.ContainsName(name))
					return 0;
				return ItemID.Search.GetId(name);
			}
			return ModLoader.GetMod(this.mod)?.GetItem(this.name)?.item.type ?? 0;
		}
	}

	internal class JSONItemConverter : TypeConverter
	{
		// Overrides the CanConvertFrom method of TypeConverter.
		// The ITypeDescriptorContext interface provides the context for the
		// conversion. Typically, this interface is used at design time to
		// provide information about the design-time container.
		public override bool CanConvertFrom(ITypeDescriptorContext context,
		   Type sourceType)
		{
			if (sourceType == typeof(string))
			{
				return true;
			}
			return base.CanConvertFrom(context, sourceType);
		}

		// Overrides the ConvertFrom method of TypeConverter.
		public override object ConvertFrom(ITypeDescriptorContext context,
		   CultureInfo culture, object value)
		{
			if (value is string)
			{
				// ModNames can't have spaces, but ItemNames can I think.
				string[] v = ((string)value).Split(new char[] { ' ' }, 2);
				return new JSONItem(v[0], v[1]);
			}
			return base.ConvertFrom(context, culture, value);
		}

		// Overrides the ConvertTo method of TypeConverter.
		public override object ConvertTo(ITypeDescriptorContext context,
		   CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string))
			{
				JSONItem item = (JSONItem)value;
				return $"{item.mod} {item.name}";
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}
	}

	class UIModConfigItemDefinitionItem : UIModConfigItem
	{
		private List<UIModConfigItemDefinitionChoice> items;
		private bool updateNeeded;
		private bool itemSelectionExpanded;

		private Func<JSONItem> _GetValue;
		private Action<JSONItem> _SetValue;
		private UIModConfigItemDefinitionChoice itemChoice;
		private UIPanel chooserPanel;
		private NestedUIGrid chooserGrid;
		private UIFocusInputTextField chooserFilter;
		private UIFocusInputTextField chooserFilterMod;

		private float itemScale = 0.5f;

		public UIModConfigItemDefinitionItem(PropertyFieldWrapper memberInfo, object item, ref int i, IList<JSONItem> array2 = null, int index = -1) : base(memberInfo, item, (IList)array2)
		{
			Height.Set(30f, 0f);

			_GetValue = () => DefaultGetValue();
			_SetValue = (JSONItem value) => DefaultSetValue(value);

			//itemChoice = new UIModConfigItemDefinitionChoice(_GetValue()?.GetID() ?? 0, 0.5f);
			itemChoice = new UIModConfigItemDefinitionChoice(_GetValue(), 0.5f);
			itemChoice.Top.Set(2f, 0f);
			itemChoice.Left.Set(-30, 1f);
			itemChoice.OnClick += (a, b) =>
			{
				itemSelectionExpanded = !itemSelectionExpanded;
				updateNeeded = true;
			};
			Append(itemChoice);

			chooserPanel = new UIPanel();
			chooserPanel.Top.Set(30, 0);
			chooserPanel.Height.Set(200, 0);
			chooserPanel.Width.Set(0, 1);
			chooserPanel.BackgroundColor = Color.CornflowerBlue;

			UIPanel textBoxBackgroundA = new UIPanel();
			textBoxBackgroundA.Width.Set(160, 0f);
			textBoxBackgroundA.Height.Set(30, 0f);
			textBoxBackgroundA.Top.Set(-6, 0);
			textBoxBackgroundA.PaddingTop = 0;
			textBoxBackgroundA.PaddingBottom = 0;
			chooserFilter = new UIFocusInputTextField("Filter by Name");
			chooserFilter.OnTextChange += (a, b) =>
			{
				updateNeeded = true;
			};
			chooserFilter.OnRightClick += (a, b) => chooserFilter.SetText("");
			chooserFilter.Width = StyleDimension.Fill;
			chooserFilter.Height.Set(-6, 1f);
			chooserFilter.Top.Set(6, 0f);
			textBoxBackgroundA.Append(chooserFilter);
			chooserPanel.Append(textBoxBackgroundA);

			UIPanel textBoxBackgroundB = new UIPanel();
			textBoxBackgroundB.CopyStyle(textBoxBackgroundA);
			textBoxBackgroundB.Left.Set(180, 0);
			chooserFilterMod = new UIFocusInputTextField("Filter by Mod");
			chooserFilterMod.OnTextChange += (a, b) =>
			{
				updateNeeded = true;
			};
			chooserFilterMod.OnRightClick += (a, b) => chooserFilterMod.SetText("");
			chooserFilterMod.Width = StyleDimension.Fill;
			chooserFilterMod.Height.Set(-6, 1f);
			chooserFilterMod.Top.Set(6, 0f);
			textBoxBackgroundB.Append(chooserFilterMod);
			chooserPanel.Append(textBoxBackgroundB);

			chooserGrid = new NestedUIGrid();
			chooserGrid.Top.Set(30, 0);
			chooserGrid.Height.Set(-30, 1);
			chooserGrid.Width.Set(-12, 1);
			chooserPanel.Append(chooserGrid);

			UIScrollbar scrollbar = new UIScrollbar();
			scrollbar.SetView(100f, 1000f);
			scrollbar.Height.Set(-30f, 1f);
			scrollbar.Top.Set(30f, 0f);
			scrollbar.Left.Pixels += 8;
			scrollbar.HAlign = 1f;
			chooserGrid.SetScrollbar(scrollbar);
			chooserPanel.Append(scrollbar);
			//Append(chooserPanel);

			UIImageButton upButton = new UIImageButton(Texture2D.FromStream(Main.instance.GraphicsDevice, Assembly.GetExecutingAssembly().GetManifestResourceStream("Terraria.ModLoader.UI.ButtonIncrement.png")));
			upButton.Recalculate();
			upButton.Top.Set(-4f, 0f);
			upButton.Left.Set(-24, 1f);
			upButton.OnClick += (a, b) =>
			{
				itemScale = Math.Min(1f, itemScale + 0.1f);
				foreach (var itemchoice in items)
				{
					itemchoice.SetScale(itemScale);
				}
			};
			chooserPanel.Append(upButton);

			UIImageButton downButton = new UIImageButton(Texture2D.FromStream(Main.instance.GraphicsDevice, Assembly.GetExecutingAssembly().GetManifestResourceStream("Terraria.ModLoader.UI.ButtonDecrement.png")));
			downButton.Top.Set(8, 0f);
			downButton.Left.Set(-24, 1f);
			downButton.OnClick += (a, b) =>
			{
				itemScale = Math.Max(0.5f, itemScale - 0.1f);
				foreach (var itemchoice in items)
				{
					itemchoice.SetScale(itemScale);
				}
			};
			chooserPanel.Append(downButton);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!updateNeeded) return;
			updateNeeded = false;
			if (itemSelectionExpanded && items == null)
			{
				items = new List<UIModConfigItemDefinitionChoice>();
				for (int i = 0; i < ItemLoader.ItemCount; i++)
				{
					int capturedID = i;
					UIModConfigItemDefinitionChoice item = new UIModConfigItemDefinitionChoice(capturedID, 0.5f);
					item.OnClick += (a, b) =>
					{
						if (capturedID >= ItemID.Count)
						{
							var moditem = ItemLoader.GetItem(capturedID);
							_SetValue(new JSONItem(moditem.mod.Name, moditem.Name));
						}
						else
							_SetValue(new JSONItem("Terraria", ItemID.Search.GetName(capturedID)));
						updateNeeded = true;
						itemSelectionExpanded = false;
					};
					items.Add(item);
				}
			}
			if (!itemSelectionExpanded)
				chooserPanel.Remove();
			else
				Append(chooserPanel);
			float newHeight = itemSelectionExpanded ? 240 : 30;
			Height.Set(newHeight, 0f);
			if (Parent != null && Parent is UISortableElement)
			{
				Parent.Height.Pixels = newHeight;
			}
			if (itemSelectionExpanded)
			{
				var passed = new List<UIModConfigItemDefinitionChoice>();
				foreach (var item in items)
				{
					if (ItemID.Sets.Deprecated[item.itemType])
						continue;
					// Should this be the localized item name?
					if (Lang.GetItemNameValue(item.itemType).IndexOf(chooserFilter.currentString, StringComparison.OrdinalIgnoreCase) == -1)
						continue;
					string modname = "Terraria";
					if (item.itemType > ItemID.Count)
					{
						modname = ItemLoader.GetItem(item.itemType).mod.DisplayName; // or internal name?
					}
					if (modname.IndexOf(chooserFilterMod.currentString, StringComparison.OrdinalIgnoreCase) == -1)
						continue;
					passed.Add(item);
				}
				chooserGrid.Clear();
				chooserGrid.AddRange(passed);
			}
			//itemChoice.SetItem(_GetValue()?.GetID() ?? 0);
			itemChoice.SetItem(_GetValue());
		}

		void DefaultSetValue(JSONItem text)
		{
			if (!memberInfo.CanWrite) return;
			memberInfo.SetValue(item, text);
			Interface.modConfig.SetPendingChanges();
		}

		JSONItem DefaultGetValue()
		{
			return (JSONItem)memberInfo.GetValue(item);
		}
	}

	// TODO: Override string for "Unloaded item" etc.
	internal class UIModConfigItemDefinitionChoice : UIElement
	{
		public static Texture2D defaultBackgroundTexture = Main.inventoryBack9Texture;
		public Texture2D backgroundTexture = defaultBackgroundTexture;
		internal float scale = .75f;
		public int itemType;
		public Item item;
		public string nameoverload;
		private bool unloaded;

		public UIModConfigItemDefinitionChoice(JSONItem item, float scale = .75f)
		{
			this.itemType = item?.GetID() ?? 0;
			if (item?.IsUnloaded ?? false)
			{
				nameoverload = $"{item.name} [{item.mod}] ({Language.GetTextValue("tModLoader.UnloadedItemItemName")})";
				unloaded = true;
			}
			this.item = new Item();
			this.item.SetDefaults(itemType);
			this.scale = scale;
			this.Width.Set(defaultBackgroundTexture.Width * scale, 0f);
			this.Height.Set(defaultBackgroundTexture.Height * scale, 0f);
		}

		public UIModConfigItemDefinitionChoice(Item item, float scale = .75f)
		{
			this.scale = scale;
			this.item = item;
			this.itemType = item.type;
			this.Width.Set(defaultBackgroundTexture.Width * scale, 0f);
			this.Height.Set(defaultBackgroundTexture.Height * scale, 0f);
		}

		public UIModConfigItemDefinitionChoice(int itemType, float scale = .75f)
		{
			this.scale = scale;
			this.item = new Item();
			item.SetDefaults(itemType);
			this.itemType = itemType;
			this.Width.Set(defaultBackgroundTexture.Width * scale, 0f);
			this.Height.Set(defaultBackgroundTexture.Height * scale, 0f);
		}

		public void SetItem(int itemType)
		{
			this.item = new Item();
			item.SetDefaults(itemType);
			this.itemType = itemType;
			nameoverload = null;
			unloaded = false;
		}

		public void SetItem(JSONItem item)
		{
			nameoverload = null;
			unloaded = false;
			this.itemType = item?.GetID() ?? 0;
			if (item?.IsUnloaded ?? false)
			{
				unloaded = true;
				nameoverload = $"{item.name} [{item.mod}] ({Language.GetTextValue("tModLoader.UnloadedItemItemName")})";
			}
			this.item = new Item();
			this.item.SetDefaults(itemType);
		}

		public void SetScale(float scale)
		{
			this.scale = scale;
			this.Width.Set(defaultBackgroundTexture.Width * scale, 0f);
			this.Height.Set(defaultBackgroundTexture.Height * scale, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (item != null)
			{
				CalculatedStyle dimensions = base.GetInnerDimensions();
				Rectangle rectangle = dimensions.ToRectangle();
				spriteBatch.Draw(backgroundTexture, dimensions.Position(), null, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				if (!item.IsAir || unloaded)
				{
					int type = unloaded ? ItemID.Count : this.item.type;
					Texture2D itemTexture = Main.itemTexture[type];
					Rectangle rectangle2;
					if (Main.itemAnimations[type] != null)
					{
						rectangle2 = Main.itemAnimations[type].GetFrame(itemTexture);
					}
					else
					{
						rectangle2 = itemTexture.Frame(1, 1, 0, 0);
					}
					Color newColor = Color.White;
					float pulseScale = 1f;
					ItemSlot.GetItemLight(ref newColor, ref pulseScale, item, false);
					int height = rectangle2.Height;
					int width = rectangle2.Width;
					float drawScale = 1f;
					float availableWidth = (float)defaultBackgroundTexture.Width * scale;
					if (width > availableWidth || height > availableWidth)
					{
						if (width > height)
						{
							drawScale = availableWidth / width;
						}
						else
						{
							drawScale = availableWidth / height;
						}
					}
					drawScale *= scale;
					Vector2 vector = backgroundTexture.Size() * scale;
					Vector2 position2 = dimensions.Position() + vector / 2f - rectangle2.Size() * drawScale / 2f;
					Vector2 origin = rectangle2.Size() * (pulseScale / 2f - 0.5f);

					if (ItemLoader.PreDrawInInventory(item, spriteBatch, position2, rectangle2, item.GetAlpha(newColor),
						item.GetColor(Color.White), origin, drawScale * pulseScale))
					{
						spriteBatch.Draw(itemTexture, position2, new Rectangle?(rectangle2), item.GetAlpha(newColor), 0f, origin, drawScale * pulseScale, SpriteEffects.None, 0f);
						if (item.color != Color.Transparent)
						{
							spriteBatch.Draw(itemTexture, position2, new Rectangle?(rectangle2), item.GetColor(Color.White), 0f, origin, drawScale * pulseScale, SpriteEffects.None, 0f);
						}
					}
					ItemLoader.PostDrawInInventory(item, spriteBatch, position2, rectangle2, item.GetAlpha(newColor),
						item.GetColor(Color.White), origin, drawScale * pulseScale);
					if (ItemID.Sets.TrapSigned[type])
					{
						spriteBatch.Draw(Main.wireTexture, dimensions.Position() + new Vector2(40f, 40f) * scale, new Rectangle?(new Rectangle(4, 58, 8, 8)), Color.White, 0f, new Vector2(4f), 1f, SpriteEffects.None, 0f);
					}
					if (IsMouseHovering)
					{
						UIModConfig.tooltip = $"{item.Name} [{item.modItem?.mod.Name ?? "Terraria"}]";
						if (!string.IsNullOrEmpty(nameoverload))
							UIModConfig.tooltip = nameoverload;
					}
				}
				else
				{
					if (IsMouseHovering)
					{
						UIModConfig.tooltip = "Nothing";
						if(!string.IsNullOrEmpty(nameoverload))
							UIModConfig.tooltip = nameoverload;
					}
				}
			}
		}

		public override int CompareTo(object obj)
		{
			UIModConfigItemDefinitionChoice other = obj as UIModConfigItemDefinitionChoice;
			return itemType.CompareTo(other.itemType);
		}
	}
}
