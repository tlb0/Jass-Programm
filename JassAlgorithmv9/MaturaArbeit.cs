using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Microsoft.Office.Interop.Excel;
using static System.Net.Mime.MediaTypeNames;
using static JassAlgorithm.Program;

namespace MaturaArbeit
{
	internal class Program
	{
		//Basic information
		readonly static string[] strategies = new string[4] { "RankedMinSearch", "MinSearch", "RankedParanoiaSearch", "ParanoiaSearch" };
		readonly static Dictionary<string, Tuple<int, int>> trumpDict = new(){
				{"6", Tuple.Create(9,0)},
				{"7", Tuple.Create(10,0)},
				{"8", Tuple.Create(11,0)},
				{"10", Tuple.Create(12,10)},
				{"Q", Tuple.Create(13,3)},
				{"K", Tuple.Create(14,4)},
				{"A", Tuple.Create(15,11)},
				{"9", Tuple.Create(16,14)},
				{"J", Tuple.Create(17,20)}
				};
		readonly static Dictionary<string, Tuple<int, int>> cardDict = new(){
				{"6", Tuple.Create(0,0)},
				{"7", Tuple.Create(1,0)},
				{"8", Tuple.Create(2,0)},
				{"9", Tuple.Create(3,0)},
				{"10", Tuple.Create(4,10)},
				{"J", Tuple.Create(5,2)},
				{"Q", Tuple.Create(6,3)},
				{"K", Tuple.Create(7,4)},
				{"A", Tuple.Create(8,11)}
				};
		readonly static string[] values = { "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
		readonly static string[] suits = { "♣", "♦", "♥", "♠" };
		static int trump = 0;

		// For calculating the searchdepth
		readonly static float[] factor = new float[36]
				{
					9, 4.291631f, 3.583935f, 3.252434f,
					8, 4.2062f, 3.604539f,3.28301f,
					7, 3.712244f, 3.431832f, 3.237821f,
					6, 3.327017f, 3.181992f, 3.073752f,
					5, 3, 3, 3,
					4, 4, 4, 4,
					3, 3, 3, 3,
					2, 2, 2, 2,
					1, 1, 1, 1
				};
		readonly static Random random = new();
		static List<Card> deck = new();

		// Stats for the players: Profiles takes an int between 0 and 3, Agressiveness takes a float between 0 and 1
		static readonly int[] profiles = new int[4] { 0, 0, 0, 0 };
		static readonly float[] agressiveness = new float[4] { 0f, 0f, 0f, 0f };
		static Stats stats = new (profiles, agressiveness, strategies);
		
		// Change Searchdepth with maximum Leafnodes
		static readonly int maxLeafNodes = 20000;

		static Stopwatch stopwatch = new();
		static void Main(string[] args)
		{
			bool run = true;
			while (run)
			{
				string input = Console.ReadLine()!;
				switch (input)
				{
					case "ai":
						AIGame();
						break;
					case "exit":
						run = false;
						break;
				}
			}
		}
		static void AIGame()
		{
			Console.WriteLine("How many games should be played?");
			string input = Console.ReadLine()!;
			if (int.TryParse(input, out int num) == true && num >= 1)
			{
				stopwatch.Start();
				for (int i = 0; i < num; i++)
				{
					Console.WriteLine($"Game {i + 1}...");
					GameState game = new(true);
					game.AiGame(true);
				}
				stopwatch.Stop();
				Console.WriteLine($"Execution Time: {stopwatch.ElapsedMilliseconds} ms " +
					$"or ca. {stopwatch.ElapsedMilliseconds / num} ms per game.");
			}
			else
			{
				Console.WriteLine("Error, invalid input");
			}
		}

		internal class GameState
		{
			public List<Player> players;
			public List<int> playedCards;
			public List<Move> moves;
			public GameState()
			{
				players = new List<Player>();
				playedCards = new List<int>();
				moves = new List<Move>();
			}
			public GameState(bool changePlayerOrder) : this()

			{
				//Creates a new Game
				deck = Deck(trump);
				CreatePlayers();
				// Every player assigns their bid
				RuleOfThumbHandleBids();
				// Every player has their own mind, containing their knowledge of the game.
				CreateMinds(deck);
				// Random order of players
				if(changePlayerOrder)
				{
					ChangePlayerOrder(random.Next(0, 3));
				}
			}
			public GameState Copy()
			{
				GameState copy = new();
				copy.players = CopyPlayerList(this.players);
				copy.playedCards = new List<int>(playedCards);
				copy.moves = moves;
				return copy;
			}
			public int[] AiGame(bool print)
			{
				// choose trump
				trump = random.Next(0, 3);

				int depth = 6;
				int chosenCard = 0;
				int bestMove = 0;
				if (print)
				{
					PrintStartingScreen();
				}

				GameState subjectiveGame;

				for (int i = 0; i < 9; i++)
				{
					for (int j = 0; j < 4; j++)
					{
						if (print)
						{
							if (j == 0)
							{
								Console.WriteLine($"Round {i + 1}");
							}
							Console.WriteLine($"It's player {players[j].id + 1}'s Turn");
						}
						int currentPlayerID = players[j].id;

						// find out what cards current Player can play
						List<int> playableCards = players[j].PlayableCards(this);

						// of all possible gameStates, chose one and create a search tree
						subjectiveGame = players[j].mind.Possibility(this);
						subjectiveGame.playedCards = playedCards;
						// estimate a search depth for the tree, given a expected count of leaf nodes
						
						// depth = DynamicDepth(i*4 + j, maxLeafNodes, stats.Strategy(currentPlayerID));
						// search for the best move according to the corresponding search algorithm
						BuildAndSearch(subjectiveGame, depth, bestMove, currentPlayerID, int.MinValue, int.MaxValue);
						chosenCard = playableCards[bestMove];

						// Play the chosen card
						PlayCard(players[j], chosenCard, print);
					}
					// after everybody played their Card
					Trick(print);
				}
				if (print)
				{
					PrintScoreBoard(WinnerList());
				}
				return Scores();
			}

			public int[] RandomGame(int id)
			{
				// choose trump
				trump = random.Next(0, 3);

				int depth = 6;
				int chosenCard = 0;
				int bestMove = 0;

				GameState subjectiveGame;

				for (int i = 0; i < 36 / players.Count; i++)
				{
					for (int j = 0; j < players.Count; j++)
					{
						int currentPlayerID = players[j].id;

						// find out what cards current Player can play
						List<int> playableCards = players[j].PlayableCards(this);

						if (currentPlayerID == id)
						{
							// of all possible gameStates, chose one and create a search tree
							subjectiveGame = players[j].mind.Possibility(this);
							subjectiveGame.playedCards = playedCards;
							// estimate a search depth for the tree, given a expected count of leaf nodes

							// depth = DynamicDepth(i*4 + j, maxLeafNodes, stats.Strategy(currentPlayerID));
							// search for the best move according to the corresponding search algorithm
							BuildAndSearch(subjectiveGame, depth, bestMove, currentPlayerID, int.MinValue, int.MaxValue);
							chosenCard = playableCards[bestMove];
						}
						else
						{
							chosenCard = playableCards[random.Next(0, playableCards.Count - 1)];
						}

						// Play the chosen card
						PlayCard(players[j], chosenCard, false);
					}
					// after everybody played their Card
					Trick(false);
				}
				return Scores();
			}
			public void CreatePlayers()
			{
				for (int i = 0; i < 4; i++)
				{
					// assign id to player, add the player to the list
					players.Add(new Player(i));
					for (int j = 0; j < 9; j++)
					{
						// first player recieves cards 1-9, second player 10-18, third 19-27, forth 28-26
						players[i].hand.Add(i * 9 + j);
					}
				}
			}
			public void CreateMinds(List<Card> deck)
			{
				// Creates players "memory", knowledge, their way of playing and their aggressiveness
				foreach (Player player in players)
				{
					player.mind = new(deck, player);
				}
			}
			public void Trick(bool print)
			{
				// find out, what card was the strongest and to which player it belongs to.
				int index = StrongestCardIndex();

				if (print)
				{
					Console.WriteLine($"PLayer {players[index].id + 1} played the strongest Card: {deck[playedCards[index]].value} of {deck[playedCards[index]].suit}");
				}

				// add the tricked cards to the strongest Player
				players[index].trickedCards.AddRange(playedCards);

				// the last trick is worth 5 points more
				if (players[0].hand.Count == 0)
				{
					Card lastTrickCard = new("", "", false, 0, 5);
					deck.Add(lastTrickCard);
					players[index].trickedCards.Add(deck.Count - 1);
				}

				// update the player knowledge, the tricked cards are in a new location
				if (players[0].mind != null)
				{
					UpdateMinds("tricked", players[index].id);
				}
				moves.Add(new Move("Tricked", players[index].id, players[0].id, false));
				// the player that tricked the cards starts the new round
				ChangePlayerOrder(index);

				playedCards.Clear();
			}
			public void ChangePlayerOrder(int index)
			{
				var selectedPlayers = players.Where(player => players.IndexOf(player) >= index).ToList();
				players.RemoveRange(index, selectedPlayers.Count);
				players.InsertRange(0, selectedPlayers);
			}
			public void RuleOfThumbHandleBids()
			{
				// every trump is worth its point value * 2, each ace is worth 11 points
				foreach (Player player in players)
				{
					foreach (int card in player.hand)
					{
						if (deck[card].isTrump == true)
						{
							player.bid += deck[card].pointValue * 2;
						}
						else if (deck[card].value == "A")
						{
							player.bid += 11;
						}
					}
				}
			}
			public void EstimateBids()
			{
				// For the search algorithm
				foreach (Player player in players)
				{
					player.bid = (int)player.PointsEstimate(this);
				}
			}
			public void UpdateMinds(string action, int lastPlayerID)
			{
				// After every move, update Player Memory
				foreach (Player player in players)
				{
					player.mind?.Update(lastPlayerID, action, playedCards, players);
				}
			}
			public void PlayCard(Player player, int card, bool print)
			{
				if (print)
				{
					Console.WriteLine($"They have chosen {deck[card].Name()}");
				}
				playedCards.Add(card);
				player.hand.Remove(card);
				if (player.mind != null)
				{
					// after each move, new knowledge is acquired
					UpdateMinds("playedCard", player.id);
				}
				bool isAgressive = true;
				if (deck[card].isTrump)
				{
					// "Mit dem trumpf das Spiel zu eröffnen ist nicht ratsam"
					if (player.hand.Count == 8 && playedCards.Count == 1)
					{
						isAgressive = true;
					}
					// Ein Nell oder Ass abzustechen ist unfreundlich
					else if (deck[card].trickValue >= 7)
					{
						if (playedCards.Where(j => deck[j].isTrump == true && (deck[j].value == "9" || deck[j].value == "A")).Any())
						{
							isAgressive = true;
						}
					}
				}
				moves.Add(new Move("PlayedCard", player.id, null, isAgressive));
			}
			public int CurrentPlayerIndex()
			{
				return playedCards.Count;
			}
			public bool TrumpInGame()
			{
				// finds out if anyone has played a trump card
				bool trumpInGame = false;
				foreach (int card in playedCards)
				{
					if (deck[card].isTrump == true)
					{
						trumpInGame = true;
						break;
					}
				}
				return trumpInGame;
			}
			public int StrongestCardIndex()
			{
				// at first, consider the first card to be the strongest
				int strongestCard = 0;

				// compare first card to all other cards in pile
				for (int i = 1; i < playedCards.Count; i++)
				{
					// same suit?
					if (deck[playedCards[i]].suit == deck[playedCards[0]].suit)
					{
						// greater value?
						if (deck[playedCards[i]].trickValue > deck[playedCards[strongestCard]].trickValue)
						{
							// then this is the new strongest card
							strongestCard = i;
						}

					}
					// if not, is the other card trump?
					else if (deck[playedCards[i]].isTrump == true)
					{
						strongestCard = i;
					}
				}
				return strongestCard;
			}
			public List<Player> WinnerList()
			{
				return CopyPlayerList(players).OrderBy(player => player.Score(false, null)).ToList();
			}

			static public void PrintScoreBoard(List<Player> players)
			{
				Console.WriteLine("------------------------------------------------");
				for (int i = 0; i < players.Count; i++)
				{
					string position = "";
					switch (i)
					{
						case 0:
							position = "first";
							break;
						case 1:
							position = "second"; break;
						case 2:
							position = "third"; break;
						case 3:
							position = "last"; break;
					}
					Console.WriteLine($"In {position} place: Player {players[i].id + 1} with {players[i].bid} points bid and {players[i].Points(false, null)} points reached makes {players[i].Score(false, null)} points difference.");
				}
				Console.WriteLine("------------------------------------------------");

			}
			public void PrintStartingScreen()
			{
				Display();
				Console.WriteLine($"Trump is {suits[trump]}.");
				Console.WriteLine($"Player {players[0].id + 1} begins.");
				Console.WriteLine("Click 'ENTER' to continue.");
				Console.ReadLine();
			}
			public void Display()
			{
				foreach (Player player in players)
				{
					player.Display();
				}
				foreach (int card in playedCards)
				{
					Console.WriteLine(deck[card].Name());
				}
			}

			public int[] Scores()
			{
				// Returns a list of scores
				int[] scores = new int[4];
				foreach (Player player in players)
				{
					scores[player.id] = player.Score(false, this);
				}
				return scores;
			}
			public float PointsLeft()
			{
				return 157 - TrickedPoints();
			}
			public int TrickedPoints()
			{
				// Calculates, how many Points have been tricked
				int points = 0;
				foreach (Player player in players)
				{
					foreach (int card in player.trickedCards)
					{
						points += deck[card].pointValue;
					}
				}
				return points;
			}
			public void UndoMove(bool print)
			{
				// for the Searchalgorithm
				//Find the last move
				Move move = moves.Last();
				//Check how many Cards have been played
				if (playedCards.Count > 0)
				{
					// Last Move was a PlayedCard, return it to the player
					players[playedCards.Count - 1].hand.Add(playedCards.Last());
					playedCards.RemoveAt(playedCards.Count - 1);
				}
				else if (playedCards.Count == 0)
				{
					// Last Move was a tricked Card, meaning the tricked cards must be returned, and the right playerOrder 
					// Has to be restored
					// First Player did the trick
					Player player = players[0];
					List<int> trickedCards = player.trickedCards;
					// Return the tricked Cards in the right order!!!
					for (int i = 4; i > 0; i--)
					{
						playedCards.Add(trickedCards[^i]);
						trickedCards.RemoveAt(trickedCards.Count - i);
					}
					// Restore PlayerOrder
					ChangePlayerOrder(players.FindIndex(player => player.id == move.firstPlayerID));
				}
				if (print)
				{
					Console.WriteLine($"Undo:{moves.Last().move} Player {moves.Last().playerID + 1}");
				}
				// Delete Move from Queue
				moves.RemoveAt(moves.Count - 1);
			}
		}
		internal class Card
		{
			public string value;
			public string suit;
			public bool isTrump;
			public int trickValue;
			public int pointValue;
			public Card(string value, string suit, bool isTrump, int trickValue, int pointValue)
			{
				this.value = value;
				this.suit = suit;
				this.isTrump = isTrump;
				this.trickValue = trickValue;
				this.pointValue = pointValue;
			}

			public string Name()
			{
				return $"{value} of {suit}";
			}
		}
		internal class Player
		{
			public int id;

			public List<int> hand = new();
			public List<int> trickedCards = new();

			public int bid;

			public Mind? mind;

			public Player(int id)
			{
				this.id = id;
			}

			public Player Copy()
			{
				Player copy = new(this.id)
				{
					hand = new(this.hand),
					trickedCards = new(this.trickedCards),
					bid = this.bid
				};
				return copy;
			}

			public int Points(bool heuristic, GameState? game)
			{
				// returns the points
				int points = 0;

				foreach (int card in trickedCards)
				{
					points += deck[card].pointValue;
				}
				if (heuristic && game != null)
				{
					points += (int)PointsEstimate(game);
				}
				return points;
			}

			public string Name()
			{
				return ("Player " + (id + 1).ToString());
			}

			public float PointsEstimate(GameState? game)
			{
				// given the cards in hand, give a point estimate
				if (game == null)
				{
					return 0;
				}
				if (hand.Count == 0)
				{
					return 0;
				}

				float averageTrickProbability = 0;
				// do this process for each card in own hand
				foreach (int c in hand)
				{
					int higherSameSuitCount = 0;
					int lowerSameSuitCount = 0;
					int allCardsCount = 0;
					int trumpsCount = 0;

					//loop through all other cards that haven't been played yet
					foreach (Player player in game.players)
					{
						if (player.id != id)
						{
							foreach (int card in player.hand)
							{
								// count how many unplayed cards there are
								allCardsCount++;

								// count how many trumps there are
								if (deck[card].isTrump)
								{
									trumpsCount++;
								}

								// count how many cards there are with the same suit
								if (deck[card].suit == deck[c].suit)
								{
									// are they higher or lower than the card they are compared to?
									if (deck[card].trickValue > deck[c].trickValue)
									{
										higherSameSuitCount++;
									}
									else
									{
										lowerSameSuitCount++;
									}
								}
							}
						}
					}

					// use formula to determine how likely it is that the given card tricks
					float cardTrickProbability = TrickProbability(
												allCardsCount, trumpsCount, higherSameSuitCount, lowerSameSuitCount, deck[c].isTrump);
					//TODO: avoid isNaN!! 
					if (!float.IsNaN(cardTrickProbability))
					{
						// add the probabilities of each card together... 
						averageTrickProbability += cardTrickProbability;
					}
				}
				// divide the sum of all probabilities by the amount of cards
				averageTrickProbability /= hand.Count;

				// to get the expected value, multiply probability with the sum of all points.
				return averageTrickProbability * game.PointsLeft();
			}
			public int Score(bool heuristic, GameState? game)
			{
				return Math.Abs(bid - Points(heuristic, game));
			}

			public bool IsPositive(int points)
			{
				// check if player scored too many or too little points
				bool isPositive = (points > bid);
				return isPositive;
			}
			public List<int> PlayableCards(GameState gameState)
			{
				//Returns a list of Playable Cards
				List<int> playableCards = new();
				if (gameState.playedCards.Count > 0)
				{
					int strongestCard = gameState.playedCards[gameState.StrongestCardIndex()];

					// is the declared card trump?
					if (deck[gameState.playedCards[0]].isTrump == true)
					{
						// can i serve trump?
						if (HasSameSuit(gameState.playedCards) == true)
						{
							playableCards.AddRange(hand.Where(card => deck[card].isTrump == true).ToList());
						}
						else
						{
							playableCards = hand;
						}
					}
					else if (HasSameSuit(gameState.playedCards) == true)
					{
						// do i own trump?
						if (OwnsTrump() == true)
						{
							// has somebody else already played a trump card before me?
							if (gameState.TrumpInGame() == true)
							{
								playableCards.AddRange(hand.Where(card => deck[card].isTrump == true && deck[card].trickValue > deck[strongestCard].trickValue).ToList());
								playableCards.AddRange(hand.Where(card => deck[card].suit == deck[gameState.playedCards[0]].suit).ToList());
							}
							else
							{
								playableCards.AddRange(hand.Where(card => deck[card].isTrump == true).ToList());
								playableCards.AddRange(hand.Where(card => deck[card].suit == deck[gameState.playedCards[0]].suit).ToList());
							}
						}
						else
						{
							playableCards.AddRange(hand.Where(card => deck[card].suit == deck[gameState.playedCards[0]].suit).ToList());
						}
					}
					else
					{
						playableCards = hand;
					}
				}
				// first person can play all their cards
				else
				{
					playableCards = hand;
				}
				Sort(playableCards);
				return playableCards;
			}

			public bool HasSameSuit(List<int> playedCards)
			{
				// Check if Player has the Samesuit as the ausgespiele Farbe
				bool sameSuit = false;
				foreach (int card in hand)
				{
					if (deck[card].suit == deck[playedCards[0]].suit)
					{
						if ((deck[card].isTrump == true && deck[card].value != "J") || deck[card].isTrump == false)
						{
							sameSuit = true;
							break;
						}
					}
				}
				return sameSuit;
			}

			public bool OwnsTrump()
			{
				// Check if Player owns trump
				bool ownsTrump = false;
				foreach (int card in hand)
				{
					if (deck[card].isTrump == true)
					{
						ownsTrump = true;
						break;
					}
				}
				return ownsTrump;
			}

			public void Display()
			{
				Console.WriteLine();
				Console.WriteLine($"Player {id + 1}");
				Console.WriteLine("HandCards");
				for (int i = 0; i < hand.Count; i++)
				{
					if (i == hand.Count - 1)
					{
						Console.WriteLine(deck[hand[i]].Name());
					}
					else
					{
						Console.Write($"{deck[hand[i]].Name()}, ");
					}
				}
				Console.WriteLine("TrickedCards");
				for (int i = 0; i < trickedCards.Count; i++)
				{
					if (i == trickedCards.Count - 1)
					{
						Console.WriteLine(deck[trickedCards[i]].Name());
					}
					else
					{
						Console.Write($"{deck[trickedCards[i]].Name()}, ");
					}
				}
				Console.WriteLine("Bid");
				Console.WriteLine(bid);
			}
		}
		internal class Knowledge
		{
			// Is the Card still in the Game, what Player has which probabilities?
			public bool inGame;
			public float[] probabilities;

			public Knowledge()
			{
				inGame = true;
				probabilities = new float[4];
			}

			public Knowledge(int selfID) : this()
			{
				for (int i = 0; i < probabilities.Length; i++)
				{
					probabilities[i] = 0f;
					if (i != selfID)
					{
						probabilities[i] = 1f;
					}
				}
			}

			public Knowledge Copy()
			{
				Knowledge copy = new();
				copy.inGame = inGame;
				copy.probabilities = (float[])probabilities.Clone();
				return copy;
			}


			public void Reset()
			{
				for (int i = 0; i < 4; i++)
				{
					probabilities[i] = 0;
				}
			}
			public void Played(int playerID)
			{
				Reset();
				probabilities[playerID] = 1;
				inGame = false;
			}

			public void HorizontalProbabilities()
			{
				float sum = probabilities.Sum();
				if (sum > 1 || sum < 0.99)
				{
					for (int i = 0; i < 4; i++)
					{
						probabilities[i] = (float)Math.Round(probabilities[i] / sum, 2);
					}
				}
			}
		}
		internal class Mind
		{
			readonly int ownerID;
			// Table for All Cards and their Probabilities
			public Dictionary<int, Knowledge> cardTable;
			// Modeled Players.
			public List<Player> playerProfiles;
			public Mind(List<Card> deck, Player self)
			{
				cardTable = new Dictionary<int, Knowledge>();
				playerProfiles = new List<Player>();
				ownerID = self.id;

				for (int i = 0; i < 4; i++)
				{
					// Players do not have an identity crisis!
					if (i == ownerID)
					{
						playerProfiles.Add(self);
					}
					else
					{
						playerProfiles.Add(new Player(i)); // we don't know anything yet about the other players
					}
				}
				for (int i = 0; i < deck.Count; i++)
				{
					if (!self.hand.Contains(i))
					{
						cardTable.Add(i, new Knowledge(ownerID));
					}
				}
			}

			public void Update(int playerID, string action, List<int> playedCards, List<Player> players)
			{
				if (action == "playedCard" && playerID != ownerID)
				{
					// rewrites the card table. Card not in game anymore
					cardTable[playedCards.Last()].Played(playerID);
					/*
                        Deduction:
                        Spieler spielt angesagte karte nicht und...
                                - Angesagte Karte ist Trumpf (nicht J)
                            UND/ODER
                                - Gespielte Karte ist nicht Trumpf
                        Schlussfolgerung:
                            Spieler besitzt weder angesagte Karte noch Trumpf
                    */
					if (playedCards.Count > 1)
					{
						if (deck[playedCards[0]].suit != deck[playedCards.Last()].suit)
						{
							if (deck[playedCards[0]].isTrump && deck[playedCards[0]].value != "J")
							{
								NoSameSuit(playerID, deck[playedCards[0]].suit);
							}
							else if (!deck[playedCards.Last()].isTrump)
							{
								NoSameSuit(playerID, deck[playedCards[0]].suit);
							}
						}
					}
					// realign probabilities, so vertical sum is always equal to 9
					cardTable = VerticalProbabilities(playerID, cardTable);
				}
				else if (action == "tricked")
				{
					foreach (Player player in players)
					{
						playerProfiles[player.id].trickedCards = player.trickedCards;
					}
				}
				// every bid in players Mind is equal to zero
				ResetBids();
				foreach (var element in cardTable)
				{
					// realign the probabilities, if new knowledge is acquired add it to the subjective player profiles
					if (element.Value.inGame)
					{
						UpdateKnowledge(element.Key, element.Value);
					}
					RealignBids(element.Key, element.Value);
				}
			}

			public void NoSameSuit(int playerID, string suit)
			{
				// In case a Player does not Play the Ausgespielte Farbe
				foreach (var card in cardTable)
				{
					if (deck[card.Key].suit == suit || (deck[card.Key].isTrump && deck[card.Key].value != "J"))
					{
						if (card.Value.inGame)
						{
							card.Value.probabilities[playerID] = 0;
						}
					}
				}
			}
			public void UpdateKnowledge(int card, Knowledge knowledge)
			{
				// realign probabilities, so sum of all probabilities is always equal to 1
				knowledge.HorizontalProbabilities();
				for (int i = 0; i < 4; i++)
				{
					// if it's garuanteed that player owns a certain card, add it to players hand.
					if (knowledge.probabilities[i] >= 0.99 && !playerProfiles[i].hand.Contains(card))
					{
						playerProfiles[i].hand.Add(card);
					}
				}
			}
			public void RealignBids(int card, Knowledge knowledge)
			{
				// Guess the Bids of the other Players with the Table
				for (int i = 0; i < 4; i++)
				{
					if (i != ownerID)
					{
						playerProfiles[i].bid = 0;
						if (deck[card].isTrump == true)
						{
							playerProfiles[i].bid += (int)(deck[card].pointValue * 2 * knowledge.probabilities[i]);
						}
						else if (deck[card].value == "A")
						{
							playerProfiles[i].bid += (int)(11 * knowledge.probabilities[i]);
						}
					}
				}

			}
			public void ResetBids()
			{
				for (int i = 0; i < playerProfiles.Count; i++)
				{
					if (i != ownerID)
					{
						playerProfiles[i].bid = 0;
					}
				}
			}
			public GameState Possibility(GameState game)
			{
				// Creates one possible GameState with the Probabilities of the Cardtable
				Dictionary<int, Knowledge> dict = WaveFunctionCollapse(cardTable);
				GameState possibility = new GameState();
				foreach (Player player in game.players)
				{
					foreach (Player p in playerProfiles)
					{
						if (p.id == player.id)
						{
							// TODO: is the copy needed? Probably
							possibility.players.Add(p.Copy());
						}
					}
				}
				foreach (var element in dict)
				{
					int card = element.Key;
					Knowledge knowledge = element.Value;
					foreach (Player player in possibility.players)
					{
						if (knowledge.inGame
							&& knowledge.probabilities[player.id] == 1
							&& !possibility.players[player.id].hand.Contains(card)
							)
						{
							player.hand.Add(card);
							break;
						}
						Sort(player.hand);
					}
				}
				possibility.EstimateBids();
				return possibility;
			}
			public void Display()
			{
				DisplayCardTable(cardTable);
			}

		}
		internal class Evaluation
		{
			public int[] scores = new int[4];
			public bool[] isPositive = new bool[4];
			public int[] ranks = new int[4];

			public void Display()
			{
				if (this == null)
				{
					Console.WriteLine("null");
				}
				Console.WriteLine("--------------------");
				for (int i = 0; i < 4; i++)
				{
					Console.WriteLine($"Player {i + 1}");
					Console.WriteLine($"Score {scores[i]}");
					Console.WriteLine($"Is Positive {isPositive[i]}");
					Console.WriteLine($"Rank {ranks[i]}");
					Console.WriteLine("-----------------");
				}
			}
		}
		internal class Move
		{
			public readonly string move;
			public readonly int playerID;
			public readonly int? firstPlayerID;
			public bool isAgressive;

			public Move(string move, int playerID, int? firstPlayerID, bool isAgressive)
			{
				this.move = move;
				this.playerID = playerID;
				this.firstPlayerID = firstPlayerID;
				this.isAgressive = isAgressive;
			}

			public void Display()
			{
				Console.WriteLine($"Player {playerID + 1} {move}.");
			}
		}
		internal class Stats
		{
			int[] profiles;
			float[] agressiveness;
			string[] strategies;

			public Stats(int[] profiles, float[] agressiveness, string[] strategies)
			{
				this.profiles = profiles;
				this.agressiveness = agressiveness;
				this.strategies = strategies;
			}

			public string Strategy(int playerID)
			{
				return strategies[profiles[playerID]];
			}
			public float Agressiveness(int playerID)
			{
				return agressiveness[playerID];
			}
		}

		public static Evaluation? BuildAndSearch(GameState gameState,
				int depth, int bestMove, int optimizingPlayerID, int alpha, int beta)
		{
			bestMove = 0;
			int currentIndex = gameState.playedCards.Count;
			Player player = gameState.players[currentIndex];
			List<int> playableCards = player.PlayableCards(gameState);

			Evaluation? bestValue = null;
			bool tricked = false;
			// base case, leafnode reached
			if (depth == 0 || playableCards.Count() == 0)
			{
				return Evaluate(stats.Agressiveness(player.id), gameState);
			}
			for (int i = 0; i < playableCards.Count; i++)
			{
				int card = playableCards[i];

				// Simulate the move...
				gameState.PlayCard(player, card, false);
				if (gameState.playedCards.Count == 4)
				{
					gameState.Trick(false);
					tricked = true;
				}

				// Recurson happens here
				Evaluation? newValue = BuildAndSearch(gameState, depth - 1, bestMove, optimizingPlayerID, alpha, beta);
				// Search procedure depends on the stategy of each player
				switch (stats.Strategy(player.id))
				{
					case "ParanoiaSearch":
						if (player.id == optimizingPlayerID) // minimize difference
						{
							bestValue = ScoreMax(bestValue, newValue, optimizingPlayerID, false);
							if (bestValue != null)
							{
								beta = Math.Min(beta, bestValue.scores[optimizingPlayerID]);
							}
						}
						else // maximize difference
						{
							bestValue = ScoreMax(bestValue, newValue, optimizingPlayerID, true);
							if (bestValue != null)
							{
								alpha = Math.Max(alpha, bestValue.scores[optimizingPlayerID]);
							}
						}
						break;
					case "RankedParanoiaSearch":
						if (player.id == optimizingPlayerID) // minimize difference
						{
							bestValue = RankedMax(bestValue, newValue, optimizingPlayerID, false);
							if (bestValue != null)
							{
								beta = Math.Min(beta, bestValue.scores[optimizingPlayerID]);
							}
						}
						else // maximize difference
						{
							bestValue = RankedMax(bestValue, newValue, optimizingPlayerID, true);
							if (bestValue != null)
							{
								alpha = Math.Max(alpha, bestValue.scores[optimizingPlayerID]);
							}
						}
						break;
					case "MinNSearch":
						bestValue = ScoreMax(bestValue, newValue, player.id, false);

						break;
					case "RankedMinSearch":
						bestValue = RankedMax(bestValue, newValue, player.id, false);
						break;
				}
				// if the best value has changed, then a better move must've been found.
				if (bestValue == newValue)
				{
					bestMove = i;
				}
				if (tricked)
				{
					gameState.UndoMove(false);
					tricked = false;
				}
				gameState.UndoMove(false);
				if (beta <= alpha)
				{
					goto pruned;
				}
			}
			pruned:
			return bestValue;
		}

		public static Evaluation? Evaluate(float agressiveness, GameState gameState)
		{
			// get Evaluation of a node

			Evaluation evaluation = new();

			// if move is too agressive, do not return a value for the node
			if (gameState.moves.Last().isAgressive && agressiveness > random.NextDouble())
			{
				return null;
			}

			// get scores of each player
			foreach (Player player in gameState.players)
			{
				evaluation.scores[player.id] = player.Score(true, gameState);
				evaluation.isPositive[player.id] = player.IsPositive(player.Points(true, gameState));
			}

			// get ranks of each player
			List<Player> ranklist = gameState.WinnerList();
			for (int i = 0; i < 4; i++)
			{
				int playerID = ranklist[i].id;
				evaluation.ranks[playerID] = i;
			}
			return evaluation;
		}
		static Evaluation? ScoreMax(Evaluation? x, Evaluation? y, int playerID, bool max)
		{
			// Given a PlayerID, it returns the Min or Max Score of two Evaluations
			if (x == null)
			{
				return y;
			}
			if (y == null)
			{
				return x;
			}
			if (max)
			{
				if (x.scores[playerID] < y.scores[playerID])
				{
					return y;
				}
				if (x.scores[playerID] == y.scores[playerID] && x.isPositive[playerID])
				{
					return y;
				}
				return x;
			}
			if (x == ScoreMax(x, y, playerID, true))
			{
				return y;
			}
			return x;
		}
		static Evaluation? RankedMax(Evaluation? x, Evaluation? y, int playerID, bool max)
		{
			// Given a PlayerID, it returns the Min or Max Rank of two Evaluations
			if (x == null)
			{
				return y;
			}
			if (y == null)
			{
				return x;
			}
			if (max)
			{
				if (x.ranks[playerID] < y.ranks[playerID])
				{
					return y;
				}
				if (x.ranks[playerID] == y.ranks[playerID])
				{
					if (x.scores[playerID] < y.scores[playerID])
					{
						return y;
					}
					else if (x.scores[playerID] == y.scores[playerID])
					{
						if (y.isPositive[playerID])
						{
							return y;
						}
					}
				}
				return x;
			}
			if (x == RankedMax(x, y, playerID, true))
			{
				return y;
			}
			return x;
		}
		static void Sort(List<int> cards)
		{
			List<int> sorted = cards.OrderBy(x => deck[x].isTrump).ThenBy(x => deck[x].suit).ThenBy(x => deck[x].trickValue).ToList();
			cards = sorted;
		}
		static void Shuffle(List<Card> deck)
		{
			// this Code is copypasted from StackOverflow
			int n = deck.Count;
			while (n > 1)
			{
				n--;
				int k = random.Next(n + 1);
				(deck[n], deck[k]) = (deck[k], deck[n]);
			}
		}
		public static int DynamicDepth(int round, int maxLeafNodes, string strategy)
		{
			// returns new search depth given the expected amount of new leafNodes.
			int result = 0;
			float currentLeafNodes = 1;
			for (int i = round; i < 36; i++)
			{
				if (currentLeafNodes * factor[i] < maxLeafNodes)
				{
					currentLeafNodes *= factor[i];
					result++;
				}
				else
				{
					break;
				}
			}
			if (result >= 9)
			{
				result = 8;
			}
			return result;
		}
		public static List<Card> Deck(int trump)
		{
			// Created a new deck of Cards
			List<Card> deck = new();
			foreach (string s in suits)
			{
				foreach (string v in values)
				{
					if (suits[trump] == s)
					{
						deck.Add(new Card(v, s, true, trumpDict[v].Item1, trumpDict[v].Item2));
					}
					else
					{
						deck.Add(new Card(v, s, false, cardDict[v].Item1, cardDict[v].Item2));
					}
				}
			}

			Shuffle(deck);
			return deck;
		}

		public static void DisplayCardTable(Dictionary<int, Knowledge> cardTable)
		{
			Console.WriteLine(@"
Card    P1  P2  P3  P4");
			foreach (var element in cardTable)
			{
				Card card = deck[element.Key];
				float[] p = element.Value.probabilities;

				Console.WriteLine(@$"
{card.Name()}:  {p[0]}  {p[1]}  {p[2]}  {p[3]}");
			}
			Console.ReadLine();
		}
	
		public static Dictionary<int, Knowledge> VerticalProbabilities(int playerID, Dictionary<int, Knowledge> cardTable)
		{
			// The Total Sum of a Column is 9
			float fix = 0;
			float played = 0;
			float impossible = 0;
			float probability;
			foreach (var element in cardTable)
			{
				float val = element.Value.probabilities[playerID];
				bool inGame = element.Value.inGame;
				if (val == 1)
				{
					if (inGame)
					{
						fix++;
					}
					else
					{
						played++;
					}
				}
				else if (val == 0)
				{
					impossible++;
				}
			}
			float a = 9 - fix - played;
			float b = 27 - impossible - played;

			probability = (float)Math.Round(a / b, 2);
			if (float.IsNaN(probability))
			{
				probability = 0;
			}
			foreach (var element in cardTable)
			{
				float val = element.Value.probabilities[playerID];
				if (val != 1 && val != 0)
				{
					element.Value.probabilities[playerID] = probability;
				}
			}
			return cardTable;
		}

		public static float ProbabilityFormula(float e)
		{
			//This is an Abbreviation, so I dont't have to write this all the time
			float result = (float)(Math.Pow(e, 2) - 3 * e + 2);
			if (result >= 0)
			{
				return result;
			}
			else
			{
				return 0;
			}
		}

		public static float TrickProbability(float allCards, float trumps, float higherSameSuit, float lowerSameSuit, bool isTrump)
		{
			// See pg 38. "Neue Evaluierungsfunktion".
			float possibleCards;
			float result = 0;
			if (isTrump)
			{
				possibleCards = allCards - higherSameSuit;
				/* e^3 - 3e^2 + 2e
                 * ---------------
                 * u^3 - 3u^2 + 2u
                 */
				try
				{
					result = (float)((ProbabilityFormula(possibleCards) * possibleCards) / (ProbabilityFormula(allCards) * allCards));
				}
				catch (DivideByZeroException c)
				{
					Console.WriteLine(c);
				}
			}
			else
			{
				/* e^2 - 3e + 2       1
                 * -------------   X  -  X  (e + 3l)
                 * u^3 - 3u + 2       4
                 */
				possibleCards = (allCards - higherSameSuit - trumps);
				try
				{
					result = (float)((ProbabilityFormula(possibleCards) / (4 * (ProbabilityFormula(allCards) * allCards))) * (possibleCards + 3 * lowerSameSuit));
				}
				catch (DivideByZeroException c)
				{
					Console.WriteLine(c);
				}
			}
			return result;
		}
		public static List<Player> CopyPlayerList(List<Player> players)
		{
			List<Player> copy = new();
			foreach (Player player in players)
			{
				copy.Add(player.Copy());
			}
			return copy;
		}
		public static Dictionary<int, Knowledge> WaveFunctionCollapse(Dictionary<int, Knowledge> dict)
		{
			Dictionary<int, Knowledge> copy = dict.ToDictionary(entry => entry.Key,
											   entry => entry.Value.Copy());
			int maxPlayer = 0;
			int maxCard = dict.ElementAt(0).Key;
			float maxProb = 0.5f;

			while (maxProb != 0)
			{
				maxProb = 0;
				// iterate through the dictionary
				// find cell with lowest entropy / highest propability
				foreach (var element in copy)
				{
					int card = element.Key;
					Knowledge knowledge = element.Value;
					knowledge.HorizontalProbabilities();
					for (int i = 0; i < knowledge.probabilities.Length; i++)
					{
						if (knowledge.probabilities[i] >= 0.5)
						{
							if (knowledge.probabilities[i] > maxProb && knowledge.probabilities[i] != 1)
							{
								maxCard = card;
								maxPlayer = i;
								maxProb = knowledge.probabilities[i];
							}
						}
						else
						{
							if (1 - knowledge.probabilities[i] > maxProb && knowledge.probabilities[i] != 0)
							{
								maxCard = card;
								maxPlayer = i;
								maxProb = knowledge.probabilities[i];
							}
						}
					}
				}
				// collapse the function
				copy[maxCard].Reset();
				if (random.NextDouble() < maxProb)
				{
					copy[maxCard].probabilities[maxPlayer] = 1;
				}
				else
				{
					copy[maxCard].probabilities[maxPlayer] = 0;
				}
				// realign propabilities
				copy = VerticalProbabilities(maxPlayer, copy);
				foreach (var element in copy)
				{
					if (element.Value.inGame)
					{
						element.Value.HorizontalProbabilities();
					}
				}	
			}
			return copy;
		}
	}
}