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
using MyMediaLite.ItemRecommendation;
using MyMediaLite;
using MyMediaLite.Data;
using MyMediaLite.IO;
using Mono.Options;

namespace Baselines.Commands
{

	public class MostPopularCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		public MostPopularCommand (string training, string test) : base (training, test, typeof (MostPopular))
		{
			((MostPopular)Recommender).ByUser = false; //default
		}

		protected override void Init ()
		{
			base.Init ();
			((MostPopular)Recommender).Feedback = Feedback;
		}

		public override void Tunning ()
		{
			if (Feedback == null || Feedback.Count == 0)
				throw new Exception ("Training data can not be null");

			if (Test == null || Test.Count == 0)
				throw new Exception ("Test data can not be null");

			log.Info ("Tunning parameters");

			QueryResult result = Train (false);
			double mrr = result.GetMetric ("MRR");
			Log ("default_user=False", mrr);

			QueryResult result2 = Train (true);
			double mrr2 = result2.GetMetric ("MRR");
			Log ("default_user=True", mrr2);
		}

		QueryResult Train (bool by_user)
		{
			QueryResult result = null;
			try {
				CreateModel (typeof (MostPopular));
				((MostPopular)Recommender).ByUser = by_user;
				((MostPopular)Recommender).Feedback = Feedback;

				TimeSpan t = Wrap.MeasureTime (delegate () {
					Train ();
					result = Evaluate ();
				});

				Console.WriteLine ("Training and Evaluate model: {0} seconds", t.TotalSeconds);
				string filename = string.Format ("MostPopular-byUser{0}", by_user);
				SaveModel (string.Format ("output/model/{0}.model", filename));
				MyMediaLite.Helper.Utils.SaveRank (filename, result);

			} catch (Exception ex) {
				Console.WriteLine (string.Format ("Exception {0}:", ex.Message));
				throw ex;
			}

			return result;
		}

		public override void Evaluate (string filename)
		{
			if (!string.IsNullOrEmpty (filename)) {
				Console.WriteLine ("Loading test data");
				if (!path_test.Equals (filename, StringComparison.InvariantCultureIgnoreCase)) {
					Test = LoadTest (filename);
					TestFeedback = LoadPositiveFeedback (filename, ItemDataFileFormat.IGNORE_FIRST_LINE);
				}

				var result = Evaluate ();
				MyMediaLite.Helper.Utils.SaveRank ("mostpopular", result);
				foreach (var metric in result.Metrics) {
					var desc = "default_user=" + ((MostPopular)Recommender).ByUser;
					log.Info (string.Format ("{0}\t\t-\t{1} = {2}", desc, metric.Item1, metric.Item2));
				}
			}
		}

		void Log (string desc, double metric)
		{
			log.Info (string.Format ("{0}\t\t-\t\tMRR = {1}", desc, metric));
		}

		public override void SetupOptions (string [] args)
		{
			base.SetupOptions (args);

			var options = new OptionSet {
				{ "byuser=", v => ((MostPopular)Recommender).ByUser = bool.Parse(v)}};

			options.Parse (args);

			log.Info ("Parameters configured!");
			log.Info ("By User: " + ((MostPopular)Recommender).ByUser);
		}
	}
}
