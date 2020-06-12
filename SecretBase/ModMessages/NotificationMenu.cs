using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using StardewModdingAPI;

using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace SecretBase.ModMessages
{
	public class NotificationMenu : IClickableMenu
	{
		// Notifications from modmail
		private List<Notification> _notifications;
		private List<List<Notification>> _pages;
		private List<ClickableComponent> _notificationButtons;
		private Notification _currentNotification;
		
		// Main menu buttons
		private ClickableTextureComponent _nextButton;
		private ClickableTextureComponent _prevButton;
		private ClickableTextureComponent _letter;
		private ClickableTextureComponent _sendLetterButton;

		// Menu control
		private const int NotificationsPerPage = 3;
		private int _currentPage;
		private int _letterLine;
		private readonly bool _showMailbox = true;
		private bool _composingLetter;
		private bool _showLetter;
		private bool _showReplyPrompt;
		
		// Menu points
		private const int MenuWidth = 680;
		private const int MenuHeight = 480;
		private Point _centre;
		private const int Scale = 4;

		// Letter dimensions
		private readonly Texture2D _letterTexture;
		private readonly Rectangle _letterIconSourceRect = new Rectangle(188, 422, 16, 13);
		private readonly Rectangle _trashIconSourceRect = new Rectangle(564, 102, 16, 26);
		private const float LetterMaxScale = 1f;
		private float _letterScale;
		private const int LetterTextOffset = 140;
		private int _letterTextWidth;
		private int _letterTextXPos;
		private int[] _letterTextYPos;
		private int _mailboxButtonYPos;

		// Strings
		private ITranslationHelper i18n => ModEntry.Instance.i18n;
		private string _hoverText = "";

		// TODO: ERRORS: Viewing letters from farmers who have been removed from the world (Game1.getFarmer(...))

		public NotificationMenu()
			: base(0, 0, 0, 0, true)
		{
			_letterTexture = Game1.temporaryContent.Load<Texture2D>("LooseSprites\\letterBG");
			
			Game1.playSound("bigSelect");
			ModEntry.ModState.HasUnreadSecretMail = false;
			
			// Menu dimensions
			width = MenuWidth;
			height = MenuHeight;
			_centre = Game1.graphics.GraphicsDevice.Viewport.TitleSafeArea.Center;
			var topLeft = Utility.getTopLeftPositionForCenteringOnScreen(width, height);
			xPositionOnScreen = (int) topLeft.X;
			yPositionOnScreen = (int) topLeft.Y;

			// Populate all notifications the player currently has
			_notificationButtons = new List<ClickableComponent>();
			for (var i = 0; i < NotificationsPerPage; i++)
			{
				_notificationButtons.Add(new ClickableComponent(
					Rectangle.Empty,
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
			
			// Other button alignments
			_mailboxButtonYPos = yPositionOnScreen + height / 2 - 48 / 2;

			// Letter text line alignments
			_letterTextWidth = MenuHeight - 24;
			_letterTextXPos = _centre.X - MenuHeight / 2 + 24;
			_letterTextYPos = new[]
			{
				_centre.Y - MenuHeight / 4 - LetterTextOffset,
				_centre.Y - LetterTextOffset,
				_centre.Y + MenuHeight / 4 - LetterTextOffset,
				(int)(_centre.Y + MenuHeight / 4 * 2.5 - LetterTextOffset),
				_centre.Y + MenuHeight / 4 * 3 - LetterTextOffset
			};

			// DEBUG JUNLK
			var logMessage = "";
			for (var i = 0; i < _notifications.Count; ++i)
				logMessage += $"Notif [{i}]: Code {_notifications[i].Request}, Duration {_notifications[i].Duration}\n";
			Log.W($"Notifications:\n{logMessage}");
			logMessage = "";
			for (var i = 0; i < _notificationButtons.Count; ++i)
				logMessage += $"Notif [{i}]: Ypos {_notificationButtons[i].bounds.Y}, myID {_notificationButtons[i].myID}\n";
			Log.W($"NotificationButtons:\n{logMessage}");
			// DEBUG JUNLK

			// Buttons in the menu and letter views
			_letter = new ClickableTextureComponent(
				new Rectangle(_centre.X - MenuHeight / 2, _centre.Y - MenuWidth / 2, MenuHeight, MenuWidth),
				_letterTexture,
				new Rectangle(0, 0, 320, 180),
				0f,
				true)
			{
				myID = 53052001
			};
			_sendLetterButton = new ClickableTextureComponent(
				Rectangle.Empty,
				Game1.mouseCursors,
				Rectangle.Empty,
				Scale)
			{
				myID = 53052002
			};
			upperRightCloseButton = new ClickableTextureComponent(
				new Rectangle(xPositionOnScreen + width - 20, yPositionOnScreen - 8, 48, 48),
				Game1.mouseCursors, 
				new Rectangle(337, 494, 12, 12), 
				Scale);

			_prevButton = new ClickableTextureComponent(
				Rectangle.Empty, 
				Game1.mouseCursors,
				new Rectangle(352, 495, 12, 11),
				Scale)
			{
				myID = 53051002,
				rightNeighborID = -7777
			};
			_nextButton = new ClickableTextureComponent(
				Rectangle.Empty, 
				Game1.mouseCursors,
				new Rectangle(365, 495, 12, 11),
				Scale)
			{
				myID = 53051001
			};

			ChangeButtonFormatting();

			if (Game1.options.SnappyMenus)
			{
				populateClickableComponentList();
				snapToDefaultClickableComponent();
			}
		}

		public NotificationMenu(long composeToWhomst)
			: this()
		{
			_showMailbox = false;
			_showLetter = true;

			// Add an extra notification for composing a new request
			OpenLetter(true,
				new Notification(
				Notification.RequestCode.Requested,
				Notification.DurationCode.None,
				composeToWhomst,
				Game1.player.UniqueMultiplayerID,
				null,
				null));
		}

		private void PaginateNotifications()
		{
			_notifications = ModEntry.Instance.PendingNotifications;
			_pages = new List<List<Notification>>();

			for (var i = 0; i < _notifications.Count; ++i)
			{
				if (_pages.Count <= i / NotificationsPerPage)
					_pages.Add(new List<Notification>());
				_pages[i / NotificationsPerPage].Add(_notifications[i]);
			}

			if (_pages.Count == 0)
				_pages.Add(new List<Notification>());

			_currentPage = Math.Min(Math.Max(_currentPage, 0), _pages.Count - 1);
			Log.W($"Pagination - _notifications: {_notifications.Count}, _pages: {_pages.Count}, _currentPage: {_currentPage}");
		}

		private void ChangeButtonFormatting()
		{
			Log.D($"ChangeButtonFormatting: at {Game1.currentGameTime.TotalGameTime.TotalSeconds:00}s");

			// Send/trash button, used for compose and send for request letters or trashing response letters
			if (_composingLetter
			    || _currentNotification == null
			    || _currentNotification.Request == Notification.RequestCode.Requested)
			{
				// Send/compose icon
				_sendLetterButton.bounds = new Rectangle(
					_letter.bounds.X + _letter.bounds.Width - 96,
					_letter.bounds.Y + _letter.bounds.Height - 96,
					64, 52);
				_sendLetterButton.sourceRect = _letterIconSourceRect;
			}
			else
			{
				// Trash icon
				_sendLetterButton.bounds = new Rectangle(
					_letter.bounds.X + _letter.bounds.Width - 96,
					_letter.bounds.Y + _letter.bounds.Height - 128,
					64, 104);
				_sendLetterButton.sourceRect = _trashIconSourceRect;
			}
			
			// Previous/Next buttons, used for pages in the Mailbox or strings in the composer
			if (_composingLetter)
			{
				// Move prev/next buttons around for changing strings when composing letters
				// Y positions should match the first line in the letter
				_prevButton.bounds.X = _letter.bounds.X - 64;
				_nextButton.bounds.X = _letter.bounds.X + _letter.bounds.Width + 64 - 48;
				_prevButton.bounds.Y = _nextButton.bounds.Y = _letterTextYPos[_letterLine];
				_prevButton.bounds.Width = _nextButton.bounds.Width = 48;
				_prevButton.bounds.Height = _nextButton.bounds.Height = 44;
			}
			else
			{
				// Move prev/next buttons back into mailbox positions
				_prevButton.bounds.X = xPositionOnScreen - 64;
				_nextButton.bounds.X = xPositionOnScreen + width + 64 - 48;
				_prevButton.bounds.Y = _nextButton.bounds.Y = _mailboxButtonYPos;
				_prevButton.bounds.Width = _nextButton.bounds.Width = 48;
				_prevButton.bounds.Height = _nextButton.bounds.Height = 44;
			}

			// Notification buttons, used for mail elements and options in the reply prompt
			for (var i = 0; i < _notificationButtons.Count; ++i)
			{
				var numBtns = NotificationsPerPage;
				if (!_showReplyPrompt)
				{
					// Arrange vertically for the mailbox elements
					_notificationButtons[i].bounds = new Rectangle(
						xPositionOnScreen + 16,
						yPositionOnScreen + 16 + i * ((height - 32) / numBtns),
						width - 32,
						(height - 32) / numBtns + 4);
				}
				else
				{
					// Arrange horizontally for reply options
					numBtns = _currentNotification == null || _currentNotification.Request == Notification.RequestCode.Requested 
						? 2
						: 3;
					_notificationButtons[i].bounds = new Rectangle(
						xPositionOnScreen + 16 + i * ((width - 32) / numBtns),
						yPositionOnScreen + 16 + height / 3 + 68,
						(width - 32) / numBtns,
						(height - 32) / 3 + 8 - 32);
				}
			}
		}
		
		private void ChangeLetterLine(int letterLine, bool playSound)
		{
			if (playSound)
				Game1.playSound("smallSelect");

			// Move the next/prev buttons with the highlighted line in the letter
			_letterLine = letterLine;
			_prevButton.bounds.Y = _nextButton.bounds.Y = _letterTextYPos[_letterLine];
			Log.W($"Changed to letter line {_letterLine}");
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

		private void OpenLetter(bool playSound, Notification notification)
		{
			if (playSound)
				Game1.playSound("shwip");
			
			_currentNotification = notification;

			_showLetter = true;

			ChangeButtonFormatting();
		}

		private void CloseLetter(bool playSound)
		{
			if (playSound)
				Game1.playSound("shwip");
			
			_letterLine = 0;
			_showReplyPrompt = false;
			_composingLetter = false;
			_showLetter = false;
			_letterScale = 0f;

			_currentNotification = null;

			if (ModEntry.Instance.PendingNotifications.Count == 0)
			{
				ModEntry.Instance.RemoveNotificationButton();
			}
		}

		private void ComposeLetter(bool playSound)
		{
			Log.W($"Composing reply letter to {Game1.getFarmer(_currentNotification.Guest).Name}");
			
			if (playSound)
				Game1.playSound("shwip");

			_showReplyPrompt = false;
			_composingLetter = true;
			_letterLine = 0;

			// Reset custom letter tokens for the reply letter
			_currentNotification.SetTokens(null, GetRandomCharacterNames());
		}

		private void SendLetter(bool playSound)
		{
			Log.W($"Sending reply letter to {Game1.getFarmer(_currentNotification.Guest).Name}");

			_currentNotification.Send();

			if (playSound)
				Game1.playSound("throw");

			_composingLetter = false;

			// Remove the original request when the response is sent
			_notifications.RemoveAll(n =>
				n.Request == Notification.RequestCode.Requested
				&& n.Guest == _currentNotification.Guest);
			PaginateNotifications();
			CloseLetter(false);
		}

		private void TrashLetter(bool playSound)
		{
			Log.W($"Trashed letter from {Game1.getFarmer(_currentNotification.Owner).Name}");
			
			if (playSound)
				Game1.playSound("trashcan");
			_notifications.Remove(_currentNotification);
			PaginateNotifications();
			CloseLetter(false);
		}
		
		private string[] GetRandomCharacterNames()
		{
			var names = new string[_currentNotification.SomeoneTokens.Length];
			for (var i = 0; i < names.Length; ++i)
			{
				var retries = 25;
				while (retries --> 0) // Slide to 0
				{
					var name = GetRandomOtherCharacterName();
					if (names.Contains(name))
						continue;
					names[i] = name;
					break;
				}

				if (names[i] == "")
					names[i] = i18n.Get("letter.someone");
			}
			return names;
		}

		private string GetRandomOtherCharacterName()
		{
			var npc = "";
			for (var i = 0; i < 25; ++i)
			{
				try
				{
					npc = Game1.getCharacterFromName(Game1.NPCGiftTastes.Keys.ToArray()
						[Game1.random.Next(Game1.NPCGiftTastes.Keys.Count)]).displayName;
				}
				catch (NullReferenceException)
				{
					continue;
				}
				break;
			}

			var player = Game1.getAllFarmers().FirstOrDefault(f
				=> f.UniqueMultiplayerID != _currentNotification.Guest
				   && f.UniqueMultiplayerID != _currentNotification.Owner)?.displayName;

			return Game1.random.NextDouble() < 0.75d
				? npc ?? player
				: player ?? npc;
		}
		
		public override void snapToDefaultClickableComponent()
		{
			currentlySnappedComponent = getComponentWithID(0);
			snapCursorToCurrentSnappedComponent();
		}
		
		public override void performHoverAction(int x, int y)
		{
			_hoverText = "";

			if (_showLetter)
			{
				_sendLetterButton.tryHover(x, y, _currentNotification.Request == Notification.RequestCode.Requested
					? 0.5f
					: 0.2f);

				if (_sendLetterButton.containsPoint(x, y))
				{
					_hoverText = i18n.Get(_composingLetter
						? "notification.send_inspect"
						: _currentNotification.Request == Notification.RequestCode.Requested 
							? "notification.compose_inspect"
							: "notification.trashcan_inspect");

					if (!_sendLetterButton.containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
					{
						Game1.playSound(_currentNotification.Request == Notification.RequestCode.Requested
							? "dwop"
							: "trashcanlid");
					}
				}
			}

			base.performHoverAction(x, y);

			if (!_showLetter || _showReplyPrompt)
			{
				for (var i = 0; i < _notificationButtons.Count; i++)
				{
					if (_pages.Count > 0
					    && _pages[0].Count > i
					    && _notificationButtons[i].containsPoint(x, y)
					    && !_notificationButtons[i].containsPoint(Game1.getOldMouseX(), Game1.getOldMouseY()))
					{
						Game1.playSound("Cowboy_gunshot");
					}
				}
			}

			_nextButton.tryHover(x, y, 0.2f);
			_prevButton.tryHover(x, y, 0.2f);
		}

		public override void receiveLeftClick(int x, int y, bool playSound = true)
		{
			base.receiveLeftClick(x, y, playSound);
			
			if (Game1.activeClickableMenu == null)
				return;

			if (_showLetter && Math.Abs(LetterMaxScale - _letterScale) < 0.001f)
			{
				if (_sendLetterButton.containsPoint(x, y))
				{
					Log.W("CurrentNotification Request: " + _currentNotification.Request
					                                      + ", Duration: " + _currentNotification.Duration);

					if (_currentNotification.Request == Notification.RequestCode.Requested)
						_showReplyPrompt = true;
					else if (_composingLetter)
						SendLetter(true);
					else
						TrashLetter(true);
				}
				else 
				{
					if (_composingLetter)
					{
						if (_prevButton.bounds.Contains(x, y))
						{
							receiveKeyPress(Keys.Left);
						}
						else if (_nextButton.bounds.Contains(x, y))
						{
							receiveKeyPress(Keys.Right);
						}
						else if (!_letter.bounds.Contains(x, y))
						{
							CloseLetter(true);
						}
						else
						{
							for (var i = 0; i < _letterTextYPos.Length; ++i)
							{
								if (new Rectangle(_letterTextXPos, _letterTextYPos[i], _letterTextWidth, MenuHeight / 4)
									.Contains(x, y))
								{
									Log.W($"Clicked on letter line {i}");
									ChangeLetterLine(i, true);
									break;
								}
							}
						}
					}
					else if (_showReplyPrompt)
					{
						if (_currentNotification.Request == Notification.RequestCode.Requested)
						{
							var request = Notification.RequestCode.Requested;
							if (_notificationButtons[0].containsPoint(x, y))
								request = Notification.RequestCode.Allowed;
							else if (_notificationButtons[1].containsPoint(x, y))
								request = Notification.RequestCode.Denied;

							if (request != Notification.RequestCode.Requested)
							{
								Game1.playSound("smallSelect");

								// Create a new working notification for our response, based on the request letter
								_currentNotification = new Notification(_currentNotification)
								{
									Request = request
								};
							}
							else
							{
								CloseLetter(true);
							}
						}
						else if (_currentNotification.Duration == Notification.DurationCode.None)
						{
							var duration = Notification.DurationCode.None;
							if (_notificationButtons[0].containsPoint(x, y))
								duration = Notification.DurationCode.Once;
							else if (_notificationButtons[1].containsPoint(x, y))
								duration = Notification.DurationCode.Today;
							else if (_notificationButtons[2].containsPoint(x, y))
								duration = Notification.DurationCode.Always;

							if (duration != Notification.DurationCode.None)
							{
								Game1.playSound("smallSelect");

								// Start composing a letter from our working notification
								_currentNotification.Duration = duration;
								ComposeLetter(true);
							}
							else
							{
								CloseLetter(true);
							}
						}
					}
					else
					{
						CloseLetter(true);
					}
				}
			} else {
				if (_currentPage < _pages.Count - 1 && _nextButton.containsPoint(x, y))
				{
					PressNextPageButton();
				}
				else if (_currentPage > 0 && _prevButton.containsPoint(x, y))
				{
					PressPreviousPageButton();
				}
				else
				{
					for (var i = 0; i < _notificationButtons.Count; i++)
					{
						var actualIndex = i * _currentPage + i;

						if (_pages.Count <= 0 || _pages[_currentPage].Count <= i || !_notificationButtons[i].containsPoint(x, y))
							continue;
					
						Log.W($"Opened letter at i: {i} ai: {actualIndex}");
						OpenLetter(true, _notifications[actualIndex]);
						return;
					}

					Game1.playSound("bigDeSelect");
					exitThisMenu();
				}
			}

			ChangeButtonFormatting();
		}

		public override void receiveRightClick(int x, int y, bool playSound = true)
		{
		}
		
		public override void receiveGamePadButton(Buttons b)
		{
			if (b == Buttons.RightTrigger && _currentPage < _pages.Count - 1)
				PressNextPageButton();
			else if (b == Buttons.LeftTrigger && _currentPage > 0)
				PressPreviousPageButton();

			ChangeButtonFormatting();
		}

		public override void receiveKeyPress(Keys key)
		{
			base.receiveKeyPress(key);

			switch (key)
			{
				case Keys.W:
				case Keys.S:
				case Keys.Up:
				case Keys.Down:
					if (_composingLetter)
					{
						ChangeLetterLine(key == Keys.Up || key == Keys.W
							? Math.Max(_letterLine - 1, 0)
							: Math.Min(_letterLine + 1, 3), true);
					}
					break;
				case Keys.A:
				case Keys.Left:
					if (_composingLetter)
					{
						Game1.playSound("Cowboy_gunshot");
						--_currentNotification.MessageTokens[_letterLine];
						if (_currentNotification.MessageTokens[_letterLine] < 0)
							_currentNotification.MessageTokens[_letterLine] = 9;
					}
					break;
				case Keys.D:
				case Keys.Right:
					if (_composingLetter)
					{
						Game1.playSound("Cowboy_gunshot");
						++_currentNotification.MessageTokens[_letterLine];
						if (_currentNotification.MessageTokens[_letterLine] > 9)
							_currentNotification.MessageTokens[_letterLine] = 0;
					}
					break;
			}

			if ((readyToClose()
			     || Game1.options.doesInputListContain(Game1.options.menuButton, key)) 
			    && Game1.options.doesInputListContain(Game1.options.journalButton, key))
			{
				Game1.exitActiveMenu();
				Game1.playSound("bigDeSelect");
				if (ModEntry.Instance.PendingNotifications.Count == 0)
				{
					ModEntry.Instance.RemoveNotificationButton();
				}
			}

			ChangeButtonFormatting();
		}

		public override void update(GameTime time)
		{
			// Open up the letter
			if (!_showLetter)
				return;

			if (!(_letterScale < LetterMaxScale))
				return;

			_letterScale += time.ElapsedGameTime.Milliseconds * 0.003f * LetterMaxScale;
			if (_letterScale >= LetterMaxScale)
				_letterScale = LetterMaxScale;
		}

		public override void draw(SpriteBatch b)
		{
			// Screen blackout
			b.Draw(
				Game1.fadeToBlackRect,
				Game1.graphics.GraphicsDevice.Viewport.Bounds,
				Color.Black * 0.75f);

			// Letter contents
			if (_showLetter)
			{
				b.Draw(
					_letterTexture,
					new Rectangle(
						(int)(_centre.X + MenuHeight / 2f * _letterScale),
						(int)((_centre.Y * 2f - MenuWidth) / 2f * _letterScale),
						(int)(MenuWidth * _letterScale),
						(int)(MenuHeight * _letterScale)),
					_letter.sourceRect,
					Color.White,
					1.5708f,
					Vector2.Zero,
					SpriteEffects.None,
					0.88f);

				if (!_showReplyPrompt && Math.Abs(LetterMaxScale - _letterScale) < 0.001f)
				{
					// All the lines of text in the letter
					for (var i = 0; i < _letterTextYPos.Length; ++i)
					{
						// Get our translation strings by building the keys
						var str = i switch
						{
							// Opening greeting and recipient's name
							0 => "letter.intro_format",
							// First line, "letter.(request||allowed||denied).(number)"
							1 => "letter."
								 + _currentNotification.Request switch
								 {
									 Notification.RequestCode.Requested => "request.",
									 Notification.RequestCode.Allowed => "allowed.",
									 Notification.RequestCode.Denied => "denied."
								 } 
								 + _currentNotification.MessageTokens[1],
							// Second line, "letter.(duration||owner).(number)"
							2 => "letter."
							     + (_currentNotification.Request == Notification.RequestCode.Requested
								     ? "duration."
								     : "owner.") 
							     + _currentNotification.MessageTokens[2],
							// Closing greeting
							3 => "letter.outro." + _currentNotification.MessageTokens[3],
							// Sender's name
							4 => "letter.signature",
							_ => ""
						};

						// Tokenise translation strings to fit their format
						str = i18n.Get(str, new
						{
							greeting = i18n.Get("letter.intro." + _currentNotification.MessageTokens[0]),
							recipient = (_currentNotification.Request == Notification.RequestCode.Requested
								? Game1.getFarmer(_currentNotification.Owner).Name
								: Game1.getFarmer(_currentNotification.Guest).Name),
							someone = _currentNotification.SomeoneTokens[i],
							sender = Game1.getFarmer(_currentNotification.Guest).Name,
						});

						SpriteText.drawString(b,
							str,
							_letterTextXPos,
							_letterTextYPos[i],
							999999,
							_letterTextWidth,
							MenuHeight / 4,
							// Highlight all lines in the viewer, or the current line in the composer
							_showReplyPrompt || !_composingLetter || _letterLine == i || _letterLine == 3 && i == 4
								? 1f
								: 0.5f);
					}

					// Compose/send/trash button
					_sendLetterButton.draw(b);
				}
			}

			// Letter accept/decline prompt popup
			if (_showReplyPrompt)
			{
				// Title container
				drawTextureBox(b,
					Game1.mouseCursors,
					new Rectangle(384, 373, 18, 18),
					xPositionOnScreen,
					yPositionOnScreen,
					width,
					height / 3,
					Color.White,
					Scale);

				// Options container
				drawTextureBox(b,
					Game1.mouseCursors,
					new Rectangle(384, 373, 18, 18),
					xPositionOnScreen,
					yPositionOnScreen + height / 3 + 64,
					width,
					height / 3,
					Color.White,
					Scale);

				// Accept/decline prompt
				SpriteText.drawStringHorizontallyCenteredAt(b,
					i18n.Get(
						(_currentNotification.Request == Notification.RequestCode.Requested 
							? "notification.response_prompt" 
							: "notification.duration_prompt"), new {
							guest = Game1.getFarmer(_currentNotification.Guest).Name
						}), 
					xPositionOnScreen + width / 2,
					yPositionOnScreen - 4 + height / 3 / 2 - SpriteText.characterHeight,
					999999,
					MenuWidth - 64,
					_notificationButtons[0].bounds.Height);
				
				// Option buttons
				var numBtns = _currentNotification.Request == Notification.RequestCode.Requested
					? 2
					: 3;
				for (var i = 0; i < numBtns; ++i) {
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
						Scale,
						false);
					
					// Option text
					var whichStr = "";
					if (_currentNotification.Request == Notification.RequestCode.Requested)
						whichStr = i == 0
							? "allow"
							: "deny";
					else
						whichStr = i == 0
							? "once"
							: i == 1
								? "today"
								: "always";
					SpriteText.drawStringHorizontallyCenteredAt(b,
						i18n.Get($"notification.{whichStr}_option"),
						_notificationButtons[i].bounds.Center.X,
						_notificationButtons[i].bounds.Center.Y - SpriteText.characterHeight * 3 / 2);
				}
			}
			
			// Mailbox menu
			if (_showMailbox && !_showLetter)
			{
				// Journal heading with paper scroll
				SpriteText.drawStringWithScrollCenteredAt(b,
					i18n.Get("notification.icon_inspect"),
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
					Scale * (1f - _letterScale));
			
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
						Scale,
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
						Scale,
						false,
						0.99f);
					
					// Summary
					SpriteText.drawString(b,
						_pages[_currentPage][i].Summary, 
						_notificationButtons[i].bounds.X + 96 + 4,
						_notificationButtons[i].bounds.Y + 24,
						999999,
						_notificationButtons[i].bounds.Width - 96 - 4,
						_notificationButtons[i].bounds.Height - 84 * 2 - 4 * 2 - 24 * 2);
				}

				if (_notifications.Count == 0)
				{
					// No mail
					SpriteText.drawStringHorizontallyCenteredAt(b,
						i18n.Get("notification.empty_inspect"), 
						xPositionOnScreen + width / 2,
						yPositionOnScreen - 24 + height / 2 - SpriteText.characterHeight,
						999999,
						MenuWidth - 64,
						_notificationButtons[0].bounds.Height);

				}
			}

			// Nav left/right buttons
			if (!_showLetter)
			{
				//if (_currentPage < _pages.Count - 1)
					_nextButton.draw(b);
				//if (_currentPage > 0)
					_prevButton.draw(b);
			}
			else if (_composingLetter)
			{
				_nextButton.draw(b);
				_prevButton.draw(b);
			}
			
			// Upper right close button
			base.draw(b);

			// Hover text
			if (_hoverText.Length > 0)
				drawHoverText(b, _hoverText, Game1.dialogueFont);

			// Cursor
			Game1.mouseCursorTransparency = 1f;
			drawMouse(b);
		}
	}
}
