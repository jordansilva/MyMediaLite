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
using System.Linq;
using MyMediaLite.ItemRecommendation;
using System.Collections.Generic;
using MyMediaLite;
using MyMediaLite.Data;
using System.IO;
using MyMediaLite.IO;
using Mono.Options;

namespace Baselines.Commands
{

	public class UserKNNCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		private static uint [] K_PARAMETERS = { 5, 10, 20, 30, 50, 80, 100, 200, 500, 1000 };

		public UserKNNCommand (string training, string test) : base (training, test, typeof (UserKNN))
		{
			((UserKNN)Recommender).K = 80; //default
			((UserKNN)Recommender).Correlation = MyMediaLite.Correlation.BinaryCorrelationType.Cosine;
			((UserKNN)Recommender).Weighted = false;
			((UserKNN)Recommender).Alpha = 0.5f;
			((UserKNN)Recommender).Q = 1;
		}

		protected override void Init ()
		{
			base.Init ();
			((UserKNN)Recommender).Feedback = Feedback;
		}

		public override void Tunning ()
		{
			if (Feedback == null || Feedback.Count == 0)
				throw new Exception ("Training data can not be null");

			if (Test == null || Test.Count == 0)
				throw new Exception ("Test data can not be null");

			log.Info ("Tunning K parameter");
			TunningK ();
		}

		void TunningK ()
		{
			var mrr_tunning = new List<Tuple<uint, double>> ();

			foreach (var k in K_PARAMETERS) {
				QueryResult result = Train (k);
				double mrr = result.GetMetric ("MRR");
				Log (k, mrr);
				mrr_tunning.Add (Tuple.Create (k, mrr));
			}

			mrr_tunning = mrr_tunning.OrderByDescending (x => x.Item2).ToList ();
			K_PARAMETERS = mrr_tunning.Select (x => x.Item1).Distinct ().Take (3).ToArray ();
			log.Debug (string.Format ("Bests K parameters: {0}", string.Join (",", K_PARAMETERS)));
		}

		QueryResult Train (uint k, float alpha = 0.5f, bool weighted = false)
		{
			TimeSpan t;
			QueryResult result = null;
			string model_filename = string.Format ("output/model/UserKNN-{0}.model", k);

			try {

				if (File.Exists (model_filename)) {
					CreateModel (typeof (UserKNN));
					((UserKNN)Recommender).Feedback = Feedback;
					LoadModel (model_filename);
					log.Info (string.Format ("Training K={0}", k));
					Console.WriteLine ("Model loaded!");
				} else {
					CreateModel (typeof (UserKNN));
					((UserKNN)Recommender).Feedback = Feedback;
					((UserKNN)Recommender).K = k;
					((UserKNN)Recommender).Alpha = alpha;
					((UserKNN)Recommender).Weighted = weighted;

					log.Info (string.Format ("Training K={0}", k));
					t = Wrap.MeasureTime (delegate () {
						Train ();
					});

					Console.WriteLine ("Training model: {0} seconds", t.TotalSeconds);
					((UserKNN)Recommender).SaveModel (string.Format ("output/model/UserKNN-{0}.model", k));
				}

				t = Wrap.MeasureTime (delegate () {
					result = Evaluate ();
				});
				Console.WriteLine ("Evaluate model: {0} seconds", t.TotalSeconds);

				string filename = string.Format ("UserKNN-{0}-a{1}-w{2}", k, alpha, weighted);
				MyMediaLite.Helper.Utils.SaveRank (filename, result);

			} catch (Exception ex) {
				Console.WriteLine (ex.Message);
			}

			return result;
		}

		void Log (uint k, double metric)
		{
			log.Info (string.Format ("k={0}\t\t-\t\tMRR = {1}", k, metric));
		}

		public override void Evaluate (string filename)
		{
			if (!string.IsNullOrEmpty (filename)) {
				Console.WriteLine ("Loading test data");
				if (string.IsNullOrEmpty (path_test) || !path_test.Equals (filename, StringComparison.InvariantCultureIgnoreCase)) {
					Test = LoadTest (filename);
					TestFeedback = LoadPositiveFeedback (filename, ItemDataFileFormat.IGNORE_FIRST_LINE);
				}

				var result = Evaluate ();
				MyMediaLite.Helper.Utils.SaveRank ("ranked-items.rank", result);
				Log (((UserKNN)Recommender).K, result.GetMetric ("MRR"));
			}
		}

		public override void SetupOptions (string [] args)
		{
			base.SetupOptions (args);

			var options = new OptionSet {
				{ "k=", v => ((UserKNN)Recommender).K = uint.Parse(v)}};

			options.Parse (args);

			log.Info ("Parameters configured!");
			log.Info ("K: " + ((UserKNN)Recommender).K);
		}
	}
}
