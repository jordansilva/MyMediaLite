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
using MyMediaLite.IO;
using System.IO;
using Mono.Options;

namespace Baselines.Commands
{

	public class ItemKNNCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		private static uint [] K_PARAMETERS = { 5, 10, 20, 30, 50, 80, 100, 500, 1000 };

		public ItemKNNCommand (string training, string test) : base (training, test, typeof (ItemKNN))
		{
			((ItemKNN)Recommender).K = 80; //default
			((ItemKNN)Recommender).Correlation = MyMediaLite.Correlation.BinaryCorrelationType.Cosine;
			((ItemKNN)Recommender).Weighted = false;
			((ItemKNN)Recommender).Alpha = 0.5f;
			((ItemKNN)Recommender).Q = 1;
		}

		protected override void Init ()
		{
			base.Init ();
			((ItemKNN)Recommender).Feedback = Feedback;
		}

		protected override IPosOnlyFeedback LoadPositiveFeedback (string path, ItemDataFileFormat file_format)
		{
			IPosOnlyFeedback feedback = ItemData.Read (path,
													   new IdentityMapping (),
													   new IdentityMapping (),
													   file_format == ItemDataFileFormat.IGNORE_FIRST_LINE);
			return feedback;
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
			//uint k = 80;
			foreach (var k in K_PARAMETERS) {
				QueryResult [] result = Train (k);

				//double mrr1 = result [0].GetMetric ("MRR");
				//Log (k, mrr1, "Rank Checked");

				double mrr2 = result [1].GetMetric ("MRR");
				Log (k, mrr2, "Rank All");

				//mrr_tunning.Add (Tuple.Create (k, mrr1));
				mrr_tunning.Add (Tuple.Create (k, mrr2));
			}

			mrr_tunning = mrr_tunning.OrderByDescending (x => x.Item2).ToList ();
			K_PARAMETERS = mrr_tunning.Select (x => x.Item1).Distinct ().Take (3).ToArray ();
			log.Debug (string.Format ("Bests K parameters: {0}", string.Join (",", K_PARAMETERS)));
		}

		QueryResult [] Train (uint k, float alpha = 0.5f, bool weighted = false)
		{
			QueryResult [] result = new QueryResult [2];
			string model_filename = string.Format ("output/model/ItemKNN-{0}.model", k);
			TimeSpan t;

			try {
				if (File.Exists (model_filename)) {
					CreateModel (typeof (ItemKNN));
					((ItemKNN)Recommender).Feedback = Feedback;
					LoadModel (model_filename);
					log.Info (string.Format ("Training K={0}", k));
					Console.WriteLine ("Model loaded!");

				} else {
					CreateModel (typeof (ItemKNN));
					((ItemKNN)Recommender).Feedback = Feedback;
					((ItemKNN)Recommender).K = k;
					((ItemKNN)Recommender).Alpha = alpha;
					((ItemKNN)Recommender).Weighted = weighted;

					log.Info (string.Format ("Training K={0}", k));
					t = Wrap.MeasureTime (delegate () {
						Train ();
					});

					Console.WriteLine ("Training model: {0} seconds", t.TotalSeconds);

					((ItemKNN)Recommender).SaveModel (string.Format ("output/model/ItemKNN-{0}.model", k));
				}

				t = Wrap.MeasureTime (delegate () {
					//var eval = EvaluateItems ();
					//Console.WriteLine ("MRR: {0}", eval ["MRR"]);
					//Console.WriteLine ("prec@5: {0}", eval ["prec@5"]);
					//Console.WriteLine ("prec@10: {0}", eval ["prec@10"]);
					//Console.WriteLine ("prec@20: {0}", eval ["prec@20"]);
					//Console.WriteLine ("prec@50: {0}", eval ["prec@50"]);
					//Console.WriteLine ("prec@100: {0}", eval ["prec@100"]);
					//Console.WriteLine ("prec@500: {0}", eval ["prec@500"]);

					//result [0] = Evaluate (false);
					result [1] = Evaluate ();
				});
				Console.WriteLine ("Evaluate model: {0} seconds", t.TotalSeconds);

				string filename = string.Format ("ItemKNN-{0}-a{1}-w{2}", k, alpha, weighted);
				MyMediaLite.Helper.Utils.SaveRank (filename, result[1]);

			} catch (Exception ex) {
				Console.WriteLine (string.Format ("Exception {0}:", ex.Message));
				throw ex;
			}

			return result;
		}

		void Log (uint k, double metric, string tag)
		{
			log.Info (string.Format ("[{2}] k={0}\t\t-\t\tMRR = {1}", k, metric, tag));
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
				Log (((ItemKNN)Recommender).K, result.GetMetric ("MRR"), "All");
			}
		}

		public override void SetupOptions (string [] args)
		{
			base.SetupOptions (args);

			var options = new OptionSet {
				{ "k=", v => ((ItemKNN)Recommender).K = uint.Parse(v)}};

			options.Parse (args);

			log.Info ("Parameters configured!");
			log.Info ("K: " + ((ItemKNN)Recommender).K);
		}
	}
}
