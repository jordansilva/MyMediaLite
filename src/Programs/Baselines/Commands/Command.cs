// Copyright (C) 2012 Zeno Gantner
//
// This file is part of MyMediaLite.
//
// MyMediaLite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MyMediaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.Collections.Generic;
using System.Linq;
using MyMediaLite;
using MyMediaLite.Data;
using MyMediaLite.Eval;
using MyMediaLite.Eval.Measures;
using MyMediaLite.IO;

namespace Baselines.Commands
{
	public abstract class Command
	{
		protected string path_training;
		protected string path_test;

		public IPosOnlyFeedback Feedback { get; set; }
		public IPosOnlyFeedback TestFeedback { get; set; }
		public IList<Checkin> Test { get; set; }
		public Recommender Recommender { get; set; }
		public IMapping user_mapping;
		public IMapping item_mapping;

		public Command () { }

		public Command (Type baseline)
		{
			CreateModel (baseline);
		}

		public Command (string training, string test, Type baseline) : this (baseline)
		{
			path_training = training;
			path_test = test;

			user_mapping = new IdentityMapping ();
			item_mapping = new IdentityMapping ();

			Init ();
		}

		protected virtual void CreateModel (Type baseline)
		{
			Recommender = null;

			if (baseline != null) {
				Recommender = Activator.CreateInstance (baseline) as Recommender;
			} else
				throw new InvalidCastException ("Baseline must to implement Recommender class");
		}


		protected virtual void Init ()
		{
			if (!string.IsNullOrEmpty (path_training)) {
				Console.WriteLine ("Loading training data");
				Feedback = LoadPositiveFeedback (path_training, ItemDataFileFormat.IGNORE_FIRST_LINE);
			}

			if (!string.IsNullOrEmpty (path_test)) {
				Console.WriteLine ("Loading test data");
				Test = LoadTest (path_test);
				TestFeedback = LoadPositiveFeedback (path_test, ItemDataFileFormat.IGNORE_FIRST_LINE);
			}
		}

		protected virtual IPosOnlyFeedback LoadPositiveFeedback (string path, ItemDataFileFormat file_format)
		{
			var feedback = ItemData.Read (path, user_mapping, item_mapping,
			                              file_format == ItemDataFileFormat.IGNORE_FIRST_LINE);
			return feedback;
		}

		protected virtual IList<Checkin> LoadTest (string path)
		{
			IList<Checkin> result = new List<Checkin> ();

			try {
				result = MyMediaLite.Helper.Utils.ReadCheckins (path);
			} catch (Exception ex) {
				Console.WriteLine (ex.Message);
			}

			return result;
		}

		public void LoadModel (string path)
		{
			Recommender.LoadModel (path);
		}

		public virtual void SaveModel (string path)
		{
			Recommender.SaveModel (path);
		}

		public void Train ()
		{
			MyMediaLite.Random.Seed = 34;
			Console.WriteLine (Recommender);
			Console.WriteLine(Feedback.Statistics (TestFeedback));
			Recommender.Train ();
		}

		public virtual void SetupOptions (string [] args) { }

		public IList<Tuple<int, float>> Recommend (int user_id, RepeatedEvents repeated_events = RepeatedEvents.No)
		{
			IList<int> test_items = (TestFeedback != null) ? TestFeedback.AllItems : new int [0];
			var candidate_items = test_items.Union (Feedback.AllItems).ToList ();

			var training_user_matrix = Feedback.UserMatrix;
			var ignore_items_for_this_user = new HashSet<int> (
						repeated_events == RepeatedEvents.Yes || training_user_matrix [user_id] == null ? new int [0] : training_user_matrix [user_id]
					);

			ignore_items_for_this_user.IntersectWith (candidate_items);
			return Recommender.Recommend (user_id, n: 500, ignore_items: ignore_items_for_this_user, candidate_items: candidate_items);
		}

		public IList<Tuple<int, float>> Predict (int user, IList<int> items)
		{
			var predictions = new List<Tuple<int, float>> ();

			foreach (int item in items) {
				var rating = Predict (user, item);
				predictions.Add (rating);
			}

			predictions = predictions.OrderByDescending (x => x.Item2).ToList ();
			return predictions;
		}


		public Tuple<int, float> Predict (int user, int item)
		{
			var rating = Recommender.Predict (user, item);
			return Tuple.Create (item, rating);
		}

		public QueryResult Evaluate ()
		{
			Console.WriteLine ("Evaluating model...");
			if (Test != null && Test.Count > 0) {
				Console.WriteLine ("Evaluate Rank with candidates");
				return EvaluateRankCandidates ();
			} else {
				Console.WriteLine ("Evaluate Rank with all items");
				return EvaluateRank ();
			}
		}

		QueryResult EvaluateRank ()
		{
			var queryResult = new QueryResult (Recommender.GetType ().Name, Recommender.ToString ());

			int i = 0;
			double evaluation = 0.0f;
			double precisionAt5 = 0.0f;
			double precisionAt10 = 0.0f;
			double precisionAt50 = 0.0f;
			double precisionAt100 = 0.0f;
			var userMatrix = TestFeedback.UserMatrix;

			var numberOfEntities = (item_mapping.NumberOfEntities > 1) ? item_mapping.NumberOfEntities : TestFeedback.MaxItemID;

			foreach (var user in TestFeedback.AllUsers) {
				i++;

				var items = Recommender.Recommend (user, numberOfEntities, candidate_items: Enumerable.Range (1, numberOfEntities).ToList ());
				queryResult.Add (i, items);

				var itemsId = items.Select (x => x.Item1).ToList ();
				evaluation += ReciprocalRank.Compute (itemsId, userMatrix [user]);
				var precisions = PrecisionAndRecall.PrecisionAt (itemsId, userMatrix [user], new int[] { 5, 10, 50, 100 });
				precisionAt5 += precisions[5];
				precisionAt10 += precisions [10];
				precisionAt50 += precisions [50];
				precisionAt100 += precisions [100];
			}

			evaluation = evaluation / (i * 1.0f);
			precisionAt5 = precisionAt5 / (i * 1.0f);
			precisionAt10 = precisionAt10 / (i * 1.0f);
			precisionAt50 = precisionAt50 / (i * 1.0f);
			precisionAt100 = precisionAt100 / (i * 1.0f);

			queryResult.AddMetric ("MRR", evaluation);
			queryResult.AddMetric ("P@5", precisionAt5);
			queryResult.AddMetric ("P@10", precisionAt10);
			queryResult.AddMetric ("P@50", precisionAt50);
			queryResult.AddMetric ("P@100", precisionAt100);
			return queryResult;
		}

		QueryResult EvaluateRankCandidates ()
		{
			var queryResult = new QueryResult (Recommender.GetType ().Name, Recommender.ToString ());
			int i = 0;
			double evaluation = 0.0f;
			foreach (Checkin item in Test) {
				i++;

				var ratings = Predict (item.User, item.Candidates);
				queryResult.Add (i, item.User, item.Item, ratings);

				var itemsId = ratings.Select (x => x.Item1).ToList ();
				int [] rel = { item.Item };
				evaluation += ReciprocalRank.Compute (itemsId, rel);
			}

			evaluation = evaluation / (i * 1.0f);

			queryResult.AddMetric ("MRR", evaluation);
			return queryResult;
		}

		public ItemRecommendationEvaluationResults EvaluateItems ()
		{
			return Items.Evaluate (Recommender, TestFeedback, Feedback, n: 500);
		}


		public abstract void Tunning ();
		public abstract void Evaluate (string filename);
	}
}
