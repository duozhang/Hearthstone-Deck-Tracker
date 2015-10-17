﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hearthstone_Deck_Tracker.Annotations;
using Hearthstone_Deck_Tracker.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Utility;

namespace Hearthstone_Deck_Tracker.Stats
{
	public class CompiledStats : INotifyPropertyChanged
	{
		private static readonly CompiledStats _instance = new CompiledStats();

		public static CompiledStats Instance
		{
			get { return _instance; }
		}

		private IEnumerable<Deck> ArenaDecks
		{
			get { return DeckList.Instance.Decks.Where(x => x != null && x.IsArenaDeck); }
		}

		public IEnumerable<ArenaRun> ArenaRuns
		{
			get { return ArenaDecks.Select(x => new ArenaRun(x)).OrderByDescending(x => x.StartTime); }
		}

		public IEnumerable<ClassStats> ArenaClasses
		{
			get { return FilteredArenaRuns.GroupBy(x => x.Class).Select(x => new ClassStats(x.Key, x)).OrderBy(x => x.Class); }
		}

		public ClassStats ArenaBestClass
		{
			get { return !ArenaClasses.Any() ? null :  ArenaClasses.OrderByDescending(x => x.WinRate).First(); }
		}

		public ClassStats ArenaWorstClass
		{
			get { return !ArenaClasses.Any() ? null : ArenaClasses.OrderBy(x => x.WinRate).First(); }
		}

		public ClassStats ArenaMostPickedClass
		{
			get { return !ArenaClasses.Any() ? null : ArenaClasses.OrderByDescending(x => x.Runs).First(); }
		}

		public ClassStats ArenaLeastPickedClass
		{
			get { return !ArenaClasses.Any() ? null : ArenaClasses.OrderBy(x => x.Runs).First(); }
		}

		public ClassStats ArenaAllClasses
		{
			get { return FilteredArenaRuns.GroupBy(x => true).Select(x => new ClassStats("All", x)).FirstOrDefault(); }
		}

		public int ArenaRunsCount
		{
			get { return FilteredArenaRuns.Count(); }
		}

		public int ArenaGamesCountTotal
		{
			get { return FilteredArenaRuns.Sum(x => x.Games.Count()); }
		}

		public int ArenaGamesCountWon
		{
			get { return FilteredArenaRuns.Sum(x => x.Games.Count(g => g.Result == GameResult.Win)); }
		}

		public int ArenaGamesCountLost
		{
			get { return FilteredArenaRuns.Sum(x => x.Games.Count(g => g.Result == GameResult.Loss)); }
		}

		public double AverageWinsPerRun
		{
			get { return (double)ArenaGamesCountWon / ArenaRunsCount; }
		}

		public IEnumerable<ArenaRun> FilteredArenaRuns
		{
			get
			{
				var filtered = ArenaRuns;
				if(Config.Instance.ArenaStatsClassFilter != HeroClassStatsFilter.All)
				{
					filtered = filtered.Where(x => x.Class == Config.Instance.ArenaStatsClassFilter.ToString());
				}
				if(Config.Instance.ArenaStatsRegionFilter != RegionAll.ALL)
				{
					var region = (Region)Enum.Parse(typeof(Region), Config.Instance.ArenaStatsRegionFilter.ToString());
					filtered = filtered.Where(x => x.Games.Any(g => g.Region == region));
				}
				switch(Config.Instance.ArenaStatsTimeFrameFilter)
				{
					case DisplayedTimeFrame.AllTime:
						return filtered;
					case DisplayedTimeFrame.CurrentSeason:
						return filtered.Where(g => g.StartTime > new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));
					case DisplayedTimeFrame.ThisWeek:
						return filtered.Where(g => g.StartTime > DateTime.Today.AddDays(-((int)g.StartTime.DayOfWeek + 1)));
					case DisplayedTimeFrame.Today:
						return filtered.Where(g => g.StartTime > DateTime.Today);
					case DisplayedTimeFrame.Custom:
						var start = (Config.Instance.ArenaStatsTimeFrameCustomStart ?? DateTime.MinValue).Date;
						var end = (Config.Instance.ArenaStatsTimeFrameCustomEnd ?? DateTime.MaxValue).Date;
						return filtered.Where(g => g.EndTime.Date >= start && g.EndTime.Date <= end);
					default:
						return filtered;
				}
			}
		}

		public IEnumerable<ChartStats> ArenaPlayedClassesPercent
		{
			get
			{
				return
					FilteredArenaRuns.GroupBy(x => x.Class)
					                 .OrderBy(x => x.Key)
					                 .Select(
					                         x =>
					                         new ChartStats
					                         {
						                         Name = x.Key + " (" + Math.Round(100.0 * x.Count() / ArenaDecks.Count()) + "%)",
						                         Value = x.Count(),
						                         Brush = new SolidColorBrush(Helper.GetClassColor(x.Key, true))
					                         });
			}
		}

		public IEnumerable<ChartStats> ArenaOpponentClassesPercent
		{
			get
			{
				var opponents = FilteredArenaRuns.SelectMany(x => x.Deck.DeckStats.Games.Select(g => g.OpponentHero)).ToList();
				return
					opponents.GroupBy(x => x)
					         .OrderBy(x => x.Key)
					         .Select(
					                 g =>
					                 new ChartStats
					                 {
						                 Name = g.Key + " (" + Math.Round(100.0 * g.Count() / opponents.Count()) + "%)",
						                 Value = g.Count(),
						                 Brush = new SolidColorBrush(Helper.GetClassColor(g.Key, true))
					                 });
			}
		}


		public IEnumerable<ChartStats> ArenaWins
		{
			get
			{
				var groupedByWins =
					FilteredArenaRuns.GroupBy(x => x.Deck.DeckStats.Games.Count(g => g.Result == GameResult.Win))
					                 .Select(x => new {Wins = x.Key, Count = x.Count(), Runs = x})
					                 .ToList();
				return Enumerable.Range(0, 13).Select(n =>
				{
					var runs = groupedByWins.FirstOrDefault(x => x.Wins == n);
					if(runs == null)
						return new ChartStats {Name = n + " wins", Value = 0};
					return new ChartStats {Name = n + " wins", Value = runs.Count};
				});
			}
		}

		public IEnumerable<ChartStats>[] ArenaWinsByClass
		{
			get
			{
				var groupedByWins =
					FilteredArenaRuns.GroupBy(x => x.Deck.DeckStats.Games.Count(g => g.Result == GameResult.Win))
					                 .Select(x => new {Wins = x.Key, Count = x.Count(), Runs = x})
					                 .ToList();
				return Enumerable.Range(0, 13).Select(n =>
				{
					var runs = groupedByWins.FirstOrDefault(x => x.Wins == n);
					if(runs == null)
						return new[] {new ChartStats {Name = n.ToString(), Value = 0, Brush = new SolidColorBrush()}};
					return
						runs.Runs.GroupBy(x => x.Class)
						    .OrderBy(x => x.Key)
						    .Select(
						            x =>
						            new ChartStats
						            {
							            Name = n + " wins (" + x.Key + ")",
							            Value = x.Count(),
							            Brush = new SolidColorBrush(Helper.GetClassColor(x.Key, true))
						            });
				}).ToArray();
			}
		}

		public IEnumerable<ChartStats>[] ArenaWinLossByClass
		{
			get
			{
				var gamesGroupedByOppHero = FilteredArenaRuns.SelectMany(x => x.Deck.DeckStats.Games).GroupBy(x => x.OpponentHero);
				return Enum.GetNames(typeof(HeroClass)).Select(x =>
				{
					var classGames = gamesGroupedByOppHero.FirstOrDefault(g => g.Key == x);
					if(classGames == null)
						return new[] {new ChartStats {Name = x, Value = 0, Brush = new SolidColorBrush()}};
					return classGames.GroupBy(g => g.Result).OrderBy(g => g.Key).Select(g =>
					{
						var color = Helper.GetClassColor(x, true);
						if(g.Key == GameResult.Loss)
							color = Color.FromRgb((byte)(color.R * 0.7), (byte)(color.G * 0.7), (byte)(color.B * 0.7));
						return new ChartStats {Name = g.Key.ToString() + " vs " + x.ToString(), Value = g.Count(), Brush = new SolidColorBrush(color)};
					});

				}).ToArray();
			}
		}

		public IEnumerable<ChartStats> AvgWinsPerClass
		{
			get
			{
				return
					FilteredArenaRuns.GroupBy(x => x.Class)
					                 .Select(
					                         x =>
					                         new ChartStats
					                         {
						                         Name = x.Key,
						                         Value =
							                         Math.Round(
							                                    (double)x.Sum(d => d.Deck.DeckStats.Games.Count(g => g.Result == GameResult.Win))
							                                    / x.Count(), 1),
						                         Brush = new SolidColorBrush(Helper.GetClassColor(x.Key, true))
					                         })
					                 .OrderBy(x => x.Value);
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		[NotifyPropertyChangedInvocator]
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			var handler = PropertyChanged;
			if(handler != null)
				handler(this, new PropertyChangedEventArgs(propertyName));
		}

		public void UpdateArenaStats()
		{
			OnPropertyChanged("ArenaRuns");
			OnPropertyChanged("ArenaOpponentClassesPercent");
			OnPropertyChanged("ArenaPlayedClassesPercent");
			OnPropertyChanged("ArenaWins");
			OnPropertyChanged("AvgWinsPerClass");
			OnPropertyChanged("FilteredArenaRuns");
			OnPropertyChanged("ArenaBestClass");
		}

		public void UpdateArenaRuns()
		{
			OnPropertyChanged("ArenaRuns");
			OnPropertyChanged("FilteredArenaRuns");
		}

		public void UpdateExpensiveArenaStats()
		{
			OnPropertyChanged("ArenaWinLossByClass");
			OnPropertyChanged("ArenaWinsByClass");
		}
	}

	public class ChartStats
	{
		public string Name { get; set; }
		public double Value { get; set; }
		public Brush Brush { get; set; }
	}

	public class ArenaRun
	{
		private readonly Deck _deck;

		public Deck Deck
		{
			get { return _deck; }
		}

		public ArenaRun(Deck deck)
		{
			_deck = deck;
		}

		public string Class
		{
			get { return _deck.Class; }
		}

		public BitmapImage ClassImage
		{
			get { return _deck.ClassImage; }
		}

		public string StartTimeString
		{
			get { return StartTime == DateTime.MinValue ? "-" : StartTime.ToString("dd.MMM HH:mm"); }
		}

		public DateTime StartTime
		{
			get { return _deck.DeckStats.Games.Any() ? _deck.DeckStats.Games.Min(g => g.StartTime) : DateTime.MinValue; }
		}

		public DateTime EndTime
		{
			get { return _deck.DeckStats.Games.Any() ? _deck.DeckStats.Games.Max(g => g.EndTime) : DateTime.MinValue; }
		}

		public int Wins
		{
			get { return _deck.DeckStats.Games.Count(x => x.Result == GameResult.Win); }
		}

		public int Losses
		{
			get { return _deck.DeckStats.Games.Count(x => x.Result == GameResult.Loss); }
		}

		public int Gold
		{
			get { return _deck.ArenaReward.Gold; }
		}

		public int Dust
		{
			get { return _deck.ArenaReward.Dust; }
		}

		public int PackCount
		{
			get { return _deck.ArenaReward.Packs.Count(x => !string.IsNullOrEmpty(x)); }
		}

		public string PackString
		{
			get
			{
				var packs = _deck.ArenaReward.Packs.Where(x => !string.IsNullOrEmpty(x)).ToList();
				return packs.Any() ? packs.Aggregate((c, n) => c + ", " + n) : "None";
			}
		}

		public int CardCount
		{
			get { return _deck.ArenaReward.Cards.Count(x => x != null && !string.IsNullOrEmpty(x.CardId)); }
		}

		public string CardString
		{
			get
			{
				var cards = _deck.ArenaReward.Cards.Where(x => x != null && !string.IsNullOrEmpty(x.CardId)).ToList();
				return cards.Any()
					       ? cards.Select(x => (Database.GetCardFromId(x.CardId).LocalizedName) + (x.Golden ? " (golden)" : ""))
					              .Aggregate((c, n) => c + ", " + n) : "None";
			}
		}

		public int Duration
		{
			get { return _deck.DeckStats.Games.Sum(x => x.SortableDuration); }
		}

		public string DurationString
		{
			get { return Duration + " min"; }
		}

		public string Region
		{
			get { return _deck.DeckStats.Games.Any() ? _deck.DeckStats.Games.First().Region.ToString() : "UNKNOWN"; }
		}

		public IEnumerable<GameStats> Games
		{
			get { return _deck.DeckStats.Games; }
		}
	}

	public class ClassStats
	{
		public string Class { get; set; }

		public IEnumerable<ArenaRun> ArenaRuns { get; set; }

		public IEnumerable<MatchupStats> Matchups { get { return ArenaRuns.SelectMany(r => r.Games).GroupBy(x => x.OpponentHero).Select(x => new MatchupStats(x.Key, x)); } }

		public MatchupStats BestMatchup { get { return Matchups.OrderByDescending(x => x.WinRate).First(); } }

		public MatchupStats WorstMatchup { get { return Matchups.OrderBy(x => x.WinRate).First(); } }

		public int Runs
		{
			get { return ArenaRuns.Count(); }
		}

		public ArenaRun BestRun
		{
			get { return ArenaRuns.OrderByDescending(x => x.Wins).ThenBy(x => x.Losses).First(); }
		}

		public int Games
		{
			get { return ArenaRuns.Sum(runs => runs.Games.Count()); }
		}

		public int Wins
		{
			get { return ArenaRuns.Sum(runs => runs.Wins); }
		}

		public int Losses
		{
			get { return ArenaRuns.Sum(runs => runs.Losses); }
		}

		public double AverageWins
		{
			get { return Math.Round((double)Wins / Runs, 1); }
		}

		public double WinRate
		{
			get { return (double)Wins / (Wins + Losses); }
		}

		public double WinRatePercent
		{
			get { return Math.Round(WinRate * 100); }
		}

		public double PickedPercent
		{
			get { return Math.Round(100.0 * Runs / CompiledStats.Instance.ArenaRunsCount); }
		}

		public BitmapImage ClassImage
		{
			get { return ImageCache.GetClassIcon(Class); }
		}


		public ClassStats(string @class, IEnumerable<ArenaRun> arenaRuns)
		{
			Class = @class;
			ArenaRuns = arenaRuns;
		}

		public class MatchupStats
		{
			public IEnumerable<GameStats> Games { get; set; }

			public MatchupStats(string @class, IEnumerable<GameStats> games)
			{
				Class = @class;
				Games = games;
			}

			public int Wins
			{
				get { return Games.Count(x => x.Result == GameResult.Win); }
			}

			public int Losses
			{
				get { return Games.Count(x => x.Result == GameResult.Loss); }
			}

			public double WinRate
			{
				get { return (double)Wins / (Wins + Losses); }
			}

			public string Class { get; set; }

			public double WinRatePercent
			{
				get { return Math.Round(WinRate * 100); }
			}
		}
	}
}
