using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace SecretBase
{
	public class NotificationMenu : IClickableMenu
	{
		private List<Notification> _notifications;
		private List<List<Notification>> _pages;
		private List<ClickableComponent> _notificationButtons;
		
		private ClickableTextureComponent _nextButton;
		private ClickableTextureComponent _prevButton;
		
		private string _hoverText = "";
		private const int NotificationsPerPage = 3;
		private int _currentPage;
		private int _letterIndex;
		
		private ClickableTextureComponent _letter;
		private ClickableComponent _acceptButton;
		private ClickableComponent _declineButton;
		private ClickableComponent _onceButton;
		private ClickableComponent _todayButton;
		private ClickableComponent _alwaysButton;
		private ClickableTextureComponent _sendLetterButton;
		
		private readonly Texture2D _letterTexture;
		private bool _showLetter;
		private readonly Rectangle _letterIconSourceRect = new Rectangle(188, 422, 16, 13);

		private ITranslationHelper i18n => ModEntry.Instance.i18n;

		// todo: allow for user clicking on 'requested' notifications to bring up an accept/decline once/today/always dialogue
		// the dialogue must end with sending a ModMessage with a finalised response

		// todo: draw letter and handle letter components (scale, show)

		public NotificationMenu()
			: base(0, 0, 0, 0, true)
		{
			_letterTexture = Game1.temporaryContent.Load<Texture2D>("LooseSprites\\letterBG");

			Game1.playSound("bigSelect");
			
			width = 680;
			height = 480;
			var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(width, height);
			xPositionOnScreen = (int) topLeft.X;
			yPositionOnScreen = (int) topLeft.Y;

			_notificationButtons = new List<ClickableComponent>();
			for (var i = 0; i < NotificationsPerPage; i++)
			{
				_notificationButtons.Add(new ClickableComponent(
					new Rectangle(
						xPositionOnScreen + 16,
						yPositionOnScreen + 16 + i * ((height - 32) / NotificationsPerPage),
						width - 32,
						(height - 32) / NotificationsPerPage + 4),
					string.Concat(i))
				{
					myID = 53050000 + i,
					downNeighborID = -7777,
					upNeighborID = i > 0
						? i - 1
						: -1,
					rightNeighborID = -7777,
					leftNeighborID = -7777,
					fullyImmutable = true
				});
			}

			PaginateNotifications();

			_letter = new ClickableTextureComponent(
				new Rectangle(
					xPositionOnScreen + width / 2, yPositionOnScreen + height / 2,
					_letterTexture.Width, _letterTexture.Height),
				_letterTexture,
				new Rectangle(0, 0, 320, 180),
				0f,
				true)
			{
				myID = 53052001
			};

			_sendLetterButton = new ClickableTextureComponent(
				new Rectangle(
					_letter.bounds.X + _letter.bounds.Width - 96, _letter.bounds.Y + _letter.bounds.Height - 64,
					16, 13),
				Game1.mouseCursors,
				_letterIconSourceRect,
				4f)
			{
				myID = 53052002
			};

			upperRightCloseButton = new ClickableTextureComponent(
				new Rectangle(xPositionOnScreen + width - 20, yPositionOnScreen - 8, 48, 48),
				Game1.mouseCursors, 
				new Rectangle(337, 494, 12, 12), 
				4f);

			_prevButton = new ClickableTextureComponent(
				new Rectangle(xPositionOnScreen - 64, yPositionOnScreen + height / 2 - 48 / 2, 48, 44),
				Game1.mouseCursors,
				new Rectangle(352, 495, 12, 11),
				4f)
			{
				myID = 53051002,
				rightNeighborID = -7777
			};
			_nextButton = new ClickableTextureComponent(
				new Rectangle(xPositionOnScreen + width + 64 - 48, yPositionOnScreen + height / 2 - 48 / 2, 48, 44),
				Game1.mouseCursors,
				new Rectangle(365, 495, 12, 11),
				4f)
			{
				myID = 53051001
			};

			if (Game1.options.SnappyMenus)
			{
				populateClickableComponentList();
				snapToDefaultClickableComponent();
			}
		}

		public override void snapToDefaultClickableComponent()
		{
			currentlySnappedComponent = getComponentWithID(0);
			snapCursorToCurrentSnappedComponent();
		}

		public override void receiveGamePadButton(Buttons b)
		{
			if (b == Buttons.RightTrigger && _currentPage < _pages.Count - 1)
				PressNextPageButton();
			else if (b == Buttons.LeftTrigger && _currentPage > 0)
				PressPreviousPageButton();
		}

		private void PaginateNotifications()
		{
			_notifications = ModEntry.Instance.PendingNotifications;
			_pages = new List<List<Notification>>();

			for (var i = _notifications.Count - 1; i >= 0; i--)
			{
				var which = _notifications.Count - 1 - i;
				if (_pages.Count <= which / NotificationsPerPage)
					_pages.Add(new List<Notification>());
				_pages[which / NotificationsPerPage].Add(_notifications[i]);
			}

			if (_pages.Count == 0)
				_pages.Add(new List<Notification>());

			_currentPage = Math.Min(Math.Max(_currentPage, 0), _pages.Count - 1);
		}

		private void OpenLetter(int whichNotification)
		{
			Game1.playSound("shwip");
			_letter.scale = 0f;
			_showLetter = true;
			_letterIndex = whichNotification;
		}

		private void SendLetter()
		{

		}

		private void CloseLetter()
		{
			_showLetter = false;
			_letter.scale = 0f;
			_letterIndex = -1;
		}
		
		public override void receiveLeftClick(int x, int y, bool playSound = true)
		{
			base.receiveLeftClick(x, y, playSound);

			if (Game1.activeClickableMenu == null)
				return;

			if (_showLetter && Math.Abs(4f - _letter.scale) < 0.001f)
			{
				if (_letterTexture.Bounds.Contains(x, y))
				{
					CloseLetter();
				}
			}
			else
			{
				for (var i = 0; i < _notificationButtons.Count; i++)
				{
					if (_pages.Count <= 0 || _pages[_currentPage].Count <= i || !_notificationButtons[i].containsPoint(x, y))
						continue;

					if (_notifications[i].Request == EntryRequest.RequestCode.Requested)
					{
						OpenLetter(i);
					}
					else
					{
						Game1.playSound("coin");
						_notifications.RemoveAt(i);
						PaginateNotifications();
					}

					return;
				}
				if (_currentPage < _pages.Count - 1 && _nextButton.containsPoint(x, y))
				{
					PressNextPageButton();
					return;
				}
				if (_currentPage > 0 && _prevButton.containsPoint(x, y))
				{
					PressPreviousPageButton();
					return;
				}
			}
			
			if (!_showLetter)
			{
				if (ModEntry.Instance.PendingNotifications.Count == 0)
				{
					ModEntry.Instance.RemoveNotificationButton();
				}

				Game1.playSound("bigDeSelect");
				exitThisMenu();
			}
			else
			{
				CloseLetter();
			}
		}

		public override void receiveRightClick(int x, int y, bool playSound = true)
		{
		}

		public override void performHoverAction(int x, int y)
		{
			_hoverText = "";

			if (_showLetter)
			{
				// todo: letter hover

				if (_sendLetterButton.containsPoint(x, y))
				{
					Game1.playSound("dwop");
					_sendLetterButton.tryHover(x, y, 0.5f);
					_hoverText = i18n.Get("notification.request.inspect");
				}

				return;
			}

			base.performHoverAction(x, y);
			for (var i = 0; i < _notificationButtons.Count; i++)
			{
				var actualIndex = i * _currentPage + i;

				if (_pages.Count > 0
				    && _pages[0].Count > i
				    && _notificationButtons[i].containsPoint(x, y)
				    && !_notificationButtons[i].containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
				{
					Game1.playSound("Cowboy_gunshot");
				}

				if (actualIndex < _notifications.Count
				    && _notificationButtons[i].containsPoint(x, y))
				{
					_hoverText = _notifications[actualIndex].Request == EntryRequest.RequestCode.Requested
						? i18n.Get("notification.request.inspect")
						: i18n.Get("notification.response.inspect");
				}
			}

			_nextButton.tryHover(x, y, 0.2f);
			_prevButton.tryHover(x, y, 0.2f);
		}

		public override void receiveKeyPress(Keys key)
		{
			base.receiveKeyPress(key);
			if (readyToClose()
				&& Game1.options.doesInputListContain(Game1.options.menuButton, key)
				|| Game1.options.doesInputListContain(Game1.options.journalButton, key))
			{
				Game1.exitActiveMenu();
				Game1.playSound("bigDeSelect");
				if (ModEntry.Instance.PendingNotifications.Count == 0)
				{
					ModEntry.Instance.RemoveNotificationButton();
				}
			}
		}

		private void PressNextPageButton()
		{
			++_currentPage;
			Game1.playSound("shwip");

			if (!Game1.options.SnappyMenus || _currentPage != _pages.Count - 1)
				return;

			currentlySnappedComponent = getComponentWithID(0);
			snapCursorToCurrentSnappedComponent();
		}

		private void PressPreviousPageButton()
		{
			--_currentPage;
			Game1.playSound("shwip");

			if (!Game1.options.SnappyMenus || _currentPage != 0)
				return;

			currentlySnappedComponent = getComponentWithID(0);
			snapCursorToCurrentSnappedComponent();
		}
		
		public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
		{
		}

		public override void update(GameTime time)
		{
			// Open up the letter
			if (!_showLetter)
				return;

			if (!(_letter.scale < 4f))
				return;

			_letter.scale += time.ElapsedGameTime.Milliseconds * 0.003f * 4f;
			if (_letter.scale >= 4f)
				_letter.scale = 4f;
		}

		public override void draw(SpriteBatch b)
		{
			// Screen blackout
			b.Draw(
				Game1.fadeToBlackRect,
				Game1.graphics.GraphicsDevice.Viewport.Bounds,
				Color.Black * 0.75f);

			// Journal heading with paper scroll
			SpriteText.drawStringWithScrollCenteredAt(b,
				i18n.Get("notification.icon.inspect"),
				xPositionOnScreen + width / 2,
				yPositionOnScreen - 64);

			// Menu container
			drawTextureBox(b,
				Game1.mouseCursors,
				new Rectangle(384, 373, 18, 18),
				xPositionOnScreen,
				yPositionOnScreen,
				width,
				height,
				Color.White,
				4f);
			
			// Notification elements
			for (var i = 0; i < _notificationButtons.Count; i++)
			{
				if (_pages.Count <= 0 || _pages[_currentPage].Count <= i)
					continue;

				// Button container
				drawTextureBox(b,
					Game1.mouseCursors,
					new Rectangle(384, 396, 15, 15),
					_notificationButtons[i].bounds.X,
					_notificationButtons[i].bounds.Y,
					_notificationButtons[i].bounds.Width,
					_notificationButtons[i].bounds.Height,
					_notificationButtons[i].containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY())
						? Color.Wheat
						: Color.White,
					4f,
					false);

				// Icon
				Utility.drawWithShadow(b,
					Game1.mouseCursors,
					new Vector2(
						_notificationButtons[i].bounds.X + 16,
						_notificationButtons[i].bounds.Y + _notificationButtons[i].bounds.Height / 2 - 13 * 2 - 4),
					_letterIconSourceRect,
					Color.White,
					0f,
					Vector2.Zero,
					4f,
					false,
					0.99f);
					
				// Message
				SpriteText.drawString(b,
					_pages[_currentPage][i].Message, 
					_notificationButtons[i].bounds.X + 96 + 4,
					_notificationButtons[i].bounds.Y + 24,
					999999,
					_notificationButtons[i].bounds.Width - 96 - 4,
					_notificationButtons[i].bounds.Height - 84 * 2 - 4 * 2 - 24 * 2);
			}

			// Page nav buttons
			if (_currentPage < _pages.Count - 1)
				_nextButton.draw(b);
			if (_currentPage > 0)
				_prevButton.draw(b);
			
			// Upper right close button
			base.draw(b);

			// Letter contents
			if (_showLetter)
			{
				_letter.draw(b);

				if (Math.Abs(4f - _letter.scale) < 0.001f)
				{
					SpriteText.drawString(b,
						i18n.Get("letter.intro",
							new { guest = Game1.getFarmer(_notifications[_letterIndex].Owner).Name }),
						_letter.bounds.X - _letter.bounds.Width / 2 * 4 + 64,
						_letter.bounds.Y - _letter.bounds.Height / 2 * 4 + 64);

					SpriteText.drawString(b,
						i18n.Get("letter.outro",
							new { owner = Game1.getFarmer(_notifications[_letterIndex].Guest).Name }),
						_letter.bounds.X - _letter.bounds.Width / 2 * 4 + 64,
						_letter.bounds.Y + _letter.bounds.Height / 2 * 4 - 64);

					_sendLetterButton.draw(b);
				}
			}

			// Hover text
			if (_hoverText.Length > 0)
				drawHoverText(b, _hoverText, Game1.dialogueFont);

			// Cursor
			Game1.mouseCursorTransparency = 1f;
			drawMouse(b);
		}
	}
}
