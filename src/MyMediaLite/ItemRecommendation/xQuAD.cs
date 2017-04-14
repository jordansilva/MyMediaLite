// Copyright (C) 2015 Zeno Gantner
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
using System.Collections.Generic;
using MathNet.Numerics.LinearAlgebra;

namespace MyMediaLite.ItemRecommendation
{
	public class xQuAD : IncrementalItemRecommender
	{

		#region Fields

		protected Matrix<double> Coverage;
		protected Matrix<double> Importance;
		protected Vector<double> Ambiguity;
		//IList<IDictionary<int, int>> attribute_count_by_user;

		#endregion

		#region Properties

		/// <summary>
		/// The diversification trade-off
		/// </summary>
		public double λ { get; set; }

		/// <summary>
		/// Number of coverage features
		/// </summary>
		public int K { get; set; }

		/// <summary>
		/// User x Item x Candidates
		/// </summary>
		//FIXME: Remove FeedbackCheckins and change to 
		//	 triple (user, item, candidates) or
		//	 create a new file where each Q has K Candidates
		public IList<Data.Checkin> FeedbackCheckins { get; set; }

		/// <summary>
		/// Item attributes to create Coverage matrix
		/// </summary>
		public DataType.IBooleanMatrix ItemAttributes {
			get { return item_attributes; }
			set {
				item_attributes = value;
				K = item_attributes.NumberOfColumns;
				MaxItemID = Math.Max (MaxItemID, item_attributes.NumberOfRows - 1);
			}
		}
		DataType.IBooleanMatrix item_attributes;


		#endregion

		public XQuAD ()
		{
			λ = 0.5;
			UpdateItems = true;
		}

		public override float Predict (int user_id, int item_id)
		{
			throw new NotImplementedException ();
		}

		public Tuple<int, double> [] Predict (int user_id, int [] candidates, int depth)
		{

			int n = candidates.Count ();
			int user_idx = user_id - 1;

			var ambiguity = Ambiguity [user_idx];
			var scores = CreateVector.Dense (n, 0.0);
			var novelty = CreateVector.Dense (K, 1.0);

			double [] relevance = ComputeRelevanceFunction (candidates, n);

			List<int> rank = new List<int> ();
			while (rank.Count () < depth) {
				int maxRank = -1;
				int maxRankItem = -1;
				double maxScore = -1;

				for (int j = 0; j < depth; j++) {
					var item_id = candidates [j];
					var item_idx = item_id - 1;

					//skip already selected document
					if (rank.Contains (j))
						continue;

					double diversity = 0.0;

					// for each sub-query
					for (int i = 0; i < K; i++) {
						diversity += Importance [user_idx, i] * Coverage [item_idx, i] * novelty [i];
					}

					double score = (1 - ambiguity) * relevance [j] + ambiguity * diversity;

					if (score > maxScore) {
						maxRank = j;
						maxRankItem = item_id;
						maxScore = score;
					}
				}

				//update the score of the selected document
				scores [maxRank] = maxScore;

				//mark as selected
				rank.Add (maxRank);

				// update novelty estimations
				for (int i = 0; i < K; i++)
					novelty [i] *= 1.0 - Coverage [maxRankItem - 1, i];
			}

			for (int j = depth; j < n; j++) {
				scores [j] = (1 - ambiguity) * relevance [j];
			}

			var rank_items = new List<Tuple<int, double>> ();
			for (int i = 0; i < n; i++)
				rank_items.Add (Tuple.Create (candidates [i], scores [i]));

			var ordered_items = rank_items.OrderByDescending (x => x.Item2).ToArray ();
			return ordered_items;
		}

		public override void Train ()
		{
			InitializeCoverageMatrix ();
			InitializeImportanceMatrix ();
			InitializeAmbiguity ();

			double mrr = 0.0;
			double mrr_geo = 0.0;
			for (int i = 0; i < FeedbackCheckins.Count; i++) {
				var checkin = FeedbackCheckins [i];
				var user = checkin.User;
				var item = checkin.Item;
				var candidates = checkin.Candidates.ToArray ();

				//RR Geographic
				int [] rel = { item };
				var rr_geo = Eval.Measures.ReciprocalRank.Compute (rel, candidates);
				mrr_geo += rr_geo;

				//RR New Rank
				Tuple<int, double> [] rank = Predict (user, candidates, 10);
				var rr = Eval.Measures.ReciprocalRank.Compute (rel, rank.Select (x => x.Item1).ToList ());
				mrr += rr;

				Console.WriteLine ("RR Geo: {0} - RR xQuAD: {1}", rr_geo, rr);
			}

			mrr /= (FeedbackCheckins.Count * 1.0f);
			mrr_geo /= (FeedbackCheckins.Count * 1.0f);
			Console.WriteLine ("MRR: {0}", mrr);
			Console.WriteLine ("MRR Geo: {0}", mrr_geo);
		}

		/// <summary>
		/// Initializes the coverage matrix.
		/// </summary>
		void InitializeCoverageMatrix ()
		{
			Coverage = CreateMatrix.Dense<double> (Feedback.MaxItemID, K);
			foreach (var item in Feedback.AllItems) {
				var vector = CreateVector.Dense (item_attributes [item].Select (x => (double)x).ToArray ());
				Coverage.SetRow (item, vector);
			}

			Coverage = Coverage.NormalizeColumns (2.0);
		}

		void InitializeImportanceMatrix ()
		{
			var vector = Vector<double>.Build.Dense (K, 1.0);
			Importance = CreateMatrix.Dense (Feedback.MaxUserID, K, vector.ToArray ());
		}

		void InitializeAmbiguity ()
		{
			Ambiguity = CreateVector.Dense (Feedback.MaxUserID, λ);
		}

		protected virtual double [] ComputeRelevanceFunction (int [] candidates, int size)
		{
			var relevance = CreateVector.Dense (candidates.Count (), 1.0 / size);
			for (int i = 0; i < size; i++) {
				relevance [i] *= 1 + (1.0 / (i + 1.0));
			}
			return relevance.ToArray ();
		}

	}
}
