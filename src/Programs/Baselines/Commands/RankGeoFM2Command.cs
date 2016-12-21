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
using MyMediaLite;
using MyMediaLite.RatingPrediction;
using MyMediaLite.IO;
using MyMediaLite.Data;
using Mono.Options;

namespace Baselines.Commands
{

	public class RankGeoFM2Command : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		ITimedRatings FeedbackRatings;
		IList<POI> Items;

		public RankGeoFM2Command (string training, string test)
		{
			path_training = training;
			path_test = test;
			Init ();
		}

		protected override void CreateModel (Type baseline)
		{
			int user_count = FeedbackRatings.AllUsers.Count;
			int item_count = FeedbackRatings.AllItems.Count;
			int K = 300;
			int rangeSize = 10;

			double [,] U1 = new double [user_count, K];
			double [,] U2 = new double [user_count, K];
			double [,] L1 = new double [item_count, K];
			double [,] L2 = new double [item_count, K];
			double [,] L3 = new double [item_count, K];
			double [,] F = new double [rangeSize, K];

			Dictionary<int, int> idMapperLocations = new Dictionary<int, int> ();
			Dictionary<int, int> idMapperUser = new Dictionary<int, int> ();


			initMatrixNormal (FeedbackRatings.AllUsers, ref U1, ref idMapperUser, K);
			initMatrixNormal (FeedbackRatings.AllUsers, ref U2, ref idMapperUser, K);
			initMatrixNormal (FeedbackRatings.AllItems, ref L1, ref idMapperLocations, K);
			initMatrixNormal (FeedbackRatings.AllItems, ref L2, ref idMapperLocations, K);
			initMatrixNormal (FeedbackRatings.AllItems, ref L3, ref idMapperLocations, K);
			initMatrixNormal (rangeSize, ref F, K);

			Recommender = new WeatherContextAwareItemRecommender (U1, U2, L1, L2, L3, F,
																 idMapperLocations,
																 idMapperUser,
																 0, null);

		}

		public override void SetupOptions (string [] args)
		{
			base.SetupOptions (args);

			string user_file = null;
			string item_file = null;

			var options = new OptionSet {
				{ "user-file=",      v              => user_file        = v },
				{ "item-file=",      v              => item_file      = v }};
			options.Parse (args);

			if (!string.IsNullOrEmpty (item_file)) {
				Console.WriteLine ("Loading items data");
				Items = MyMediaLite.Helper.Utils.ReadPOIs (item_file);
			}

		}

		/// <summary>
		/// Initialize a matrix with normal distributed values with mean = 0.0 std = 0.01
		/// </summary>
		/// <param name="ids">Identifiers.</param>
		/// <param name="M">Matrix to initialize</param>
		private static void initMatrixNormal (int size, ref double [,] M, int K)
		{
			MathNet.Numerics.Distributions.Normal normalDist = new MathNet.Numerics.Distributions.Normal (0.0, 0.01);
			for (int i = 0; i < size; i++) {
				for (int j = 0; j < K; j++) {
					M [i, j] = normalDist.Sample ();
				}
			}
		}

		/// <summary>
		/// Initialize a matrix with normal distributed values with mean = 0.0 std = 0.01
		/// </summary>
		/// <param name="ids">Identifiers.</param>
		/// <param name="M">Matrix to initialize</param>
		private static void initMatrixNormal (IList<int> ids, ref double [,] M, ref Dictionary<int, int> mapper, int K)
		{
			MathNet.Numerics.Distributions.Normal normalDist = new MathNet.Numerics.Distributions.Normal (0.0, 0.01);
			int i = 0;
			foreach (int id in ids) {
				for (int j = 0; j < K; j++) {
					M [i, j] = normalDist.Sample ();
					mapper [id] = i;
				}
				i++;
			}
		}

		protected override void Init ()
		{
			if (!string.IsNullOrEmpty (path_training)) {
				Console.WriteLine ("Loading training data");
				FeedbackRatings = CustomTimedRatingData.Read (path_training,
															  new IdentityMapping (),
															  new IdentityMapping (),
															  TestRatingFileFormat.WITHOUT_RATINGS, true);
			}

			//if (!string.IsNullOrEmpty (path_test)) {
			//	Console.WriteLine ("Loading test data");
			//	Test = LoadTest (path_test);
			//}
		}

		public override void Tunning ()
		{
			//if (FeedbackRatings == null || FeedbackRatings.Count == 0)
			//	throw new Exception ("Training data can not be null");

			//if (Test == null || Test.Count == 0)
			//	throw new Exception ("Test data can not be null");
			Train (0);
		}

		QueryResult Train (int none)
		{
			bool evaluate = true;
			QueryResult result = null;
			//while (evaluate) {
			try {
				CreateModel (null);
				((WeatherContextAwareItemRecommender)Recommender).weather_aware = false;
				((WeatherContextAwareItemRecommender)Recommender).max_iter = 7000;
				((WeatherContextAwareItemRecommender)Recommender).rangeSize = 10;
				((WeatherContextAwareItemRecommender)Recommender).evaluation_at = 20;
				((WeatherContextAwareItemRecommender)Recommender).Ratings = FeedbackRatings;
				//((WeatherContextAwareItemRecommender)Recommender).Items = Items;
				TimeSpan t = Wrap.MeasureTime (delegate () {
					Train ();
					result = Evaluate ();
				});

				Console.WriteLine ("Training and Evaluate model: {0} seconds", t.TotalSeconds);
				evaluate = false;
			} catch (Exception ex) {
				throw ex;
				evaluate = true;
			}
			//}

			return result;
		}

		//void Log (uint num_factors, float regularization, float learn_rate, double metric)
		//{
		//	log.Info (string.Format ("n={0}\tl={1}\tr={2}\t\t-\t\tMRR = {3}", num_factors,
		//							 learn_rate, regularization, metric));
		//}

		public override void Evaluate (string filename)
		{
			throw new NotImplementedException ();
		}
	}
}
