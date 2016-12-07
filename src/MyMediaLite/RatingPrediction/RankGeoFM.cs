﻿// Copyright (C) 2016 Jordan Silva
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
using MyMediaLite.Data;
using MyMediaLite.DataType;
using MyMediaLite.Helper;
using System.IO;
using MyMediaLite.IO;
using System.Threading.Tasks;
using MathNet.Numerics.Distributions;

namespace MyMediaLite.RatingPrediction
{
	/// <summary>Geographical factorization method proposed by Li et al.</summary>
	/// <remarks>
	///   <para>
	///     We use the fast learning method proposed by Hu et al. (alternating least squares, ALS),
	///     and we use a global parameter to give observed values higher weights.
	///   </para>
	///   <para>
	///     Literature:
	///     <list type="bullet">
	///       <item><description>
	/// 		Xutao Li, Gao Cong, Xiao-Li Li, Tuan-Anh Nguyen Pham, and Shonali Krishnaswamy. 2015. 
	/// 		Rank-GeoFM: A Ranking based Geographical Factorization Method for Point of Interest Recommendation. 
	/// 		ACM SIGIR 2015. 
	/// 		http://dx.doi.org/10.1145/2766462.2767722
	///       </description></item>
	///     </list>
	///   </para>
	///   <para>
	///     This recommender supports incremental updates.
	///   </para>
	/// </remarks>
	public class RankGeoFM : TimeAwareRatingPredictor
	{

		#region Fields

		const string FILENAME_DISTANCE_MATRIX = @"DistanceMatrix.bin.RankGeoFM";
		const string FILENAME_DISTANCE_MATRIX_IDX = @"DistanceMatrixIndex.bin.RankGeoFM";
		const string FILENAME_U1 = @"U1.bin.RankGeoFM";
		const string FILENAME_U2 = @"U2.bin.RankGeoFM";
		const string FILENAME_L1 = @"L1.bin.RankGeoFM";
		const string FILENAME_W = @"W.bin.RankGeoFM";
		const string FILENAME_MAP_USERS = @"MapUsers.bin.RankGeoFM";
		const string FILENAME_MAP_ITEMS = @"MapItems.bin.RankGeoFM";

		Matrix<double> distanceMatrix;
		Matrix<int> distanceMatrixIndex;
		Matrix<double> W;
		double [] lossWeight;

		/// <summary>
		/// "Model Paramaeter U^(1) used to model the user's own preference. U(1) ∈ R|U|×K" [1]
		/// </summary>
		private Matrix<double> U_1;

		/// <summary>
		/// On the other hand, we introduce one extra latent factor matrix U(2) ∈ R|U|×K for users, 
		/// and employ U(2) to model the interaction between users and POIs for incorporating the geographical influence.
		/// </summary>
		private Matrix<double> U_2;

		/// <summary>
		/// "Model Paramaeter L^(1) used to model the user's own preference. L(1) ∈ R|L|×K" [1]
		/// </summary>
		private Matrix<double> L_1;

		/// <summary>
		/// Maps Database UserId to Matrix UserId in U1-U4
		/// </summary>
		Dictionary<int, int> idMapperUsers;

		/// <summary>
		/// Maps Database LocationId to Matrix LocationId in L1
		/// </summary>
		Dictionary<int, int> idMapperLocations;

		#endregion

		#region Properties

		/// <summary>
		/// Hyperparameter k_1 is the number of neighbors considered for Geographical influence
		/// </summary>
		public uint K_1 { get { return k_1; } set { k_1 = value; } }
		uint k_1 = 300;

		/// <summary>K is the number of dimensions of latent space Θ</summary> 
		public uint K { get; set; }

		/// <summary>
		/// Hyperparameter ε initialized as in [1]
		/// The Epsilon is named as maginal in the original Matlab code.
		/// </summary>
		public float ε { get; set; }

		/// <summary>
		/// Hyperparameter C initialized as in [1] eq(5), eq(6) and eq(7)
		/// The C is named as C in original Matlab code.
		/// </summary>
		public float C { get; set; }

		/// <summary>
		/// Hyperparameter Learning rate γ initialized as in [1]
		/// The Gamma is named as alpha in the original Matlab code.
		/// </summary>
		public float γ { get; set; }

		/// <summary>
		/// Hyperparameter Alpha initialize as in [1] eq(7)
		/// "As a result, tuning the hyperparameter α can balance the contributions of user-preference and geographical influence scores to the final recommendation score." [1]
		/// "We find that Rank-GeoFM perfoms the best at α = 0.2 for POI recommendation on both data, and performs the best at α=0.1 for time-aware POI recommendation on both data. " [1]
		/// The Alpha is named as FactorInf in the original Matlab code.
		/// </summary>
		public float α { get; set; }

		/// <summary>Number of maximum iterations</summary>
		public uint MaxIterations { get; set; }

		/// <summary>The POIs items.</summary>
		public IList<POI> Items { get; set; }

		#endregion

		///
		public RankGeoFM ()
		{
			K = 100;
			C = 1.0f;
			ε = 0.3f;
			γ = 0.0001f;
			α = 0.2f;
			MaxIterations = 1000;
		}

		/// <summary>
		/// Initialize configurations and data for training.
		/// </summary>
		void Initialize ()
		{
			Console.WriteLine ("{0} Computing distance matrix", DateTime.Now);
			ComputeDistanceMatrix ();

			Console.WriteLine ("{0} Creating weighted matrix", DateTime.Now);
			CreateWeightedMatrix ();

			Console.WriteLine ("{0} Creating latent factors matrix", DateTime.Now);
			CreatePreferenceMatrix ();

			Console.WriteLine ("{0} Initializing loss weight function", DateTime.Now);
			InitializeLossWeight ();
		}

		void CreatePreferenceMatrix ()
		{
			if (File.Exists (FILENAME_U1)) {
				U_1 = (Matrix<double>)FileSerializer.Deserialize (FILENAME_U1);
				U_2 = (Matrix<double>)FileSerializer.Deserialize (FILENAME_U2);
				L_1 = (Matrix<double>)FileSerializer.Deserialize (FILENAME_L1);
				idMapperUsers = (Dictionary<int, int>)FileSerializer.Deserialize (FILENAME_MAP_USERS);
				idMapperLocations = (Dictionary<int, int>)FileSerializer.Deserialize (FILENAME_MAP_ITEMS);
			} else {
				//TODO: Check MaxUserID or AllUsers.Count/AllItems.Count
				var all_places = Items.Select (x => x.Id).ToList ();

				U_1 = new Matrix<double> (Ratings.AllUsers.Count + 1, K);
				U_2 = new Matrix<double> (Ratings.AllUsers.Count + 1, K);
				L_1 = new Matrix<double> (all_places.Count + 1, K);

				idMapperUsers = new Dictionary<int, int> ();
				idMapperLocations = new Dictionary<int, int> ();

				//UNDONE: Check if is necessary these mappings
				InitMatrixNormal (Ratings.AllUsers, ref U_1, ref idMapperUsers, K);
				InitMatrixNormal (Ratings.AllUsers, ref U_2, ref idMapperUsers, K);
				InitMatrixNormal (all_places, ref L_1, ref idMapperLocations, K);
			}
		}

		#region Distance Matrix

		/// <summary>
		/// Initialize the distance matrix between all items.
		/// Store only K nearest neighbors of each item.
		/// </summary>
		void ComputeDistanceMatrix ()
		{
			if (Items == null)
				throw new Exception ("Items can not be null");

			if (File.Exists (FILENAME_DISTANCE_MATRIX)) {
				distanceMatrix = (Matrix<double>)FileSerializer.Deserialize (FILENAME_DISTANCE_MATRIX);
				distanceMatrixIndex = (Matrix<int>)FileSerializer.Deserialize (FILENAME_DISTANCE_MATRIX_IDX);
			} else {
				distanceMatrix = new Matrix<double> (Items.Count + 1, k_1);
				distanceMatrixIndex = new Matrix<int> (Items.Count + 1, k_1);

				var i = 1;
				var opts = new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32 (Math.Ceiling ((Environment.ProcessorCount * 0.95) * 1.0)) };
				Parallel.ForEach (Items, opts, item => CalculateDistancesItem (item, ref i));
				SaveDistanceMatrix ();
			}
		}

		/// <summary>
		/// Calculate POI distance between all POIs
		/// Store only K nearest neighbors of each item.
		/// </summary>
		/// <param name="item">POI</param>
		/// <param name="count">Count for print status of progress</param>
		void CalculateDistancesItem (POI item, ref int count)
		{
			var dist = new List<double> ();
			foreach (var item2 in Items) {
				if (item.Id == item2.Id)
					dist.Add (double.MaxValue);
				else
					dist.Add (DistanceHelper.Distance (item.Coordinates.Latitude, item.Coordinates.Longitude,
													   item2.Coordinates.Latitude, item2.Coordinates.Longitude));
			}

			var sorted = dist.Select ((x, i) => new KeyValuePair<double, int> (x, i))
							 .OrderBy (x => x.Key)
							 .Take ((int)k_1).ToList ();
			distanceMatrix.SetRow (item.Id, sorted.Select (x => x.Key).ToList ());
			distanceMatrixIndex.SetRow (item.Id, sorted.Select (x => x.Value).ToList ());
			dist = null;

			count++;
			if ((count % 10000) == 0) {
				Console.WriteLine ("{0} - {1}", DateTime.Now, count);
				SaveDistanceMatrix ();
			}
		}

		#endregion

		#region Creating W Matrix

		/// <summary>
		/// Creates the W matrix that contains probabilities that user visits POI l when l' has been visited.
		/// </summary>
		void CreateWeightedMatrix ()
		{
			if (File.Exists (FILENAME_W)) {
				W = (Matrix<double>)FileSerializer.Deserialize (FILENAME_W);
			} else {
				W = new Matrix<double> (Items.Count + 1, k_1);

				var count = 1;
				var opts = new ParallelOptions { MaxDegreeOfParallelism = Convert.ToInt32 (Math.Ceiling ((Environment.ProcessorCount * 0.95) * 1.0)) };
				Parallel.For (0, distanceMatrix.NumberOfRows, opts, index => ComputeItemWeightedMatrix (index, ref count));

				W.Serialize (FILENAME_W);
			}
		}

		/// <summary>
		/// Computes the weighted matrix.
		/// </summary>
		/// <param name="i">The index.</param>
		/// <param name="count">Count.</param>
		void ComputeItemWeightedMatrix (int i, ref int count)
		{
			//for (int i = 0; i < distanceMatrix.NumberOfRows; i++) {
			if (distanceMatrix.GetRow (i).Count < K)
				throw new Exception (string.Format ("Number of nearest neighbors ({0}) is less than K={1}",
												   distanceMatrix.GetRow (i).Count, K));

			var values = distanceMatrix.GetRow (i);
			var reg_values = values.Select (x => (x < 0.5 && x > 0) ? 0.5 : x).ToList ();
			var total = reg_values.Select (x => (1.0 / x)).Sum ();
			reg_values = reg_values.Select (x => ((1.0 / x) / total)).ToList ();
			W.SetRow (i, reg_values);

			count++;
			if ((count % 10000) == 0)
				Console.WriteLine ("{0} - {1}", DateTime.Now, count);
			//}
		}

		#endregion

		void InitMatrixNormal (IList<int> ids, ref Matrix<double> M, ref Dictionary<int, int> mapper, uint k)
		{
			var normalDist = new Normal (0.0, 0.01);
			int i = 0;
			foreach (int id in ids) {
				for (int j = 0; j < k; j++) {
					M [i, j] = normalDist.Sample ();
					mapper [id] = i;
				}
				i++;
			}
		}

		/// <summary>
		/// Gets the nearest neighbors of the POI.
		/// </summary>
		/// <param name="id">Id</param>
		/// <returns>The nearest neighbors.</returns>
		public IList<double> GetNearestNeighborsItem (int id)
		{
			return distanceMatrix.GetRow (id);
		}

		#region Predict Functions

		///
		public override float Predict (int user_id, int item_id)
		{
			throw new NotImplementedException ();
		}

		///
		public override float Predict (int user_id, int item_id, DateTime time)
		{
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Computes the recommendation score Eq (4)
		/// </summary>
		/// <returns>The recommendation score.</returns>
		/// <param name="user">user</param>
		/// <param name="item">location</param>
		private double ComputeRecommendationScore (int user, int item)
		{
			var user_pref = ComputeUserPreferenceScore (user, item);
			var geo_pref = ComputeGeographicalInfluenceScore (user, item);

			return user_pref + geo_pref;
		}

		double ComputeUserPreferenceScore (int user, int location)
		{
			var ind_user = idMapperUsers [user];
			var ind_loc = idMapperLocations [location];

			var u_row = U_1.GetRow (ind_user);
			var l_row = L_1.GetRow (ind_loc);

			var score = DataType.VectorExtensions.ScalarProduct (u_row, l_row);
			return score;
		}

		double ComputeGeographicalInfluenceScore (int user, int location)
		{
			var ind_user = idMapperUsers [user];
			var u_row = U_2.GetRow (ind_user);

			double [] sum = ComputeGeographicalWeightSum (location);

			var score = DataType.VectorExtensions.ScalarProduct (u_row, sum);
			return score;
		}

		/// <summary>
		/// Computes the geographical weight sum.
		/// Sum the k neighbors weight of the item × latent factors of each item
		/// </summary>
		/// <param name="location">POI</param>
		/// <returns>The geographical weight sum.</returns>
		double [] ComputeGeographicalWeightSum (int location)
		{
			double [] sum = new double [K];

			for (int i = 0; i < k_1; i++) {

				//w_ℓℓ
				var weight = W [location, i];

				//l_ℓ*
				var uid = distanceMatrixIndex [location, i];
				uid = idMapperLocations [uid];
				var l_features = L_1.GetRow (uid);

				l_features = l_features.Multiply (weight);
				sum.Add (l_features);
			}

			return sum;
		}

		#endregion

		///
		public override void Train ()
		{
			Initialize ();
			//TODO: Shuffle data

			uint iter = 1;
			//double threshold = C * Alpha;
			//double best_precision = 0.0;

			var index = ratings.RandomIndex;
			while (iter <= MaxIterations) {
				//Matrices to compare difference after each iteration
				var U_1_pre = new Matrix<double> (U_1);
				var U_2_pre = new Matrix<double> (U_2);
				var L_1_pre = new Matrix<double> (L_1);

				Iterate (index);

				var U_1_diff = DataType.MatrixExtensions.EuclideanNorm (DataType.MatrixExtensions.MatrixDifference (U_1_pre, U_1));
				var U_2_diff = DataType.MatrixExtensions.EuclideanNorm (DataType.MatrixExtensions.MatrixDifference (U_2_pre, U_2));
				var L_1_diff = DataType.MatrixExtensions.EuclideanNorm (DataType.MatrixExtensions.MatrixDifference (L_1_pre, L_1));
				var diff = U_1_diff + U_2_diff + L_1_diff;
				Console.WriteLine ("Iteration {0} - Latent Diffs: {1}", iter, diff);

				iter++;
			}

			//Saving Weighted Matrix
			SaveWeightedMatrix ();
		}

		void Iterate (IList<int> rating_indices)
		{
			int numUsers = Ratings.AllUsers.Count;
			int numItems = Ratings.AllItems.Count;

			//Iteration through all checkins
			foreach (var index in rating_indices) {
				int user = ratings.Users [index];
				int item1 = ratings.Items [index];

				var x_score = ComputeRecommendationScore (user, item1);
				var x_freq = timed_ratings.getCheckinCount (user, item1);

				IList<int> locations = new List<int> (timed_ratings.AllItems);
				locations.Shuffle ();
				locations.Remove (item1);

				//Sampling ranking (lines 5~8)
				var item2 = -1;
				var n = 0.0;
				var y_score = 0.0;
				var y_freq = 0;
				foreach (var item in locations) {
					y_score = ComputeRecommendationScore (user, item);
					y_freq = timed_ratings.getCheckinCount (user, item);
					n++;

					if (Incompatibility (x_score, x_freq, y_score, y_freq) == 1) {
						item2 = item;
						break;
					}
				}

				//Updating relevant latent factors by using SGD method (lines 9~15)
				if (item2 != -1) {
					//ƞ
					var err = Convert.ToInt32((numItems - 1) / n);
					double ƞ = E (err) * deltaFunction (x_score, y_score);
					UpdateRelevantFactors (item1, item2, user, ƞ);
				}
			}
		}

		/// <summary>
		/// Update relevant latent factors
		/// </summary>
		/// <param name="item1">POI ℓ</param>
		/// <param name="item2">POI ℓ'</param>
		/// <param name="user">User id</param>
		/// <param name="ƞ">loss ƞ</param>
		void UpdateRelevantFactors (int item1, int item2, int user, double ƞ)
		{
			var ind_user = idMapperUsers [user];
			var idx_item1 = idMapperLocations [item1];
			var idx_item2 = idMapperLocations [item2];

			//g
			var g = ComputeGeographicalWeightSum (item2).MinusWithReturn (ComputeGeographicalWeightSum (item1));

			var u1_user = U_1.GetRow (ind_user);
			var u2_user = U_2.GetRow (ind_user);
			var l1_item1 = L_1.GetRow (idx_item1);
			var l1_item2 = L_1.GetRow (idx_item2);

			//updating U_1
			//eq. U_1 = u1 - γƞ(L_l' - L_l)
			var u1_new = (l1_item2.MinusWithReturn(l1_item1));
			u1_new = u1_new.Multiply ((γ * ƞ));
			u1_new = u1_user.MinusWithReturn (u1_new);
			U_1.SetRow (ind_user, u1_new);

			//updating U_2
			//eq. U_2 = u2 - γƞg
			var u2_new = g.Multiply((γ * ƞ));
			u2_new = u2_user.MinusWithReturn (u2_new);
			U_2.SetRow (ind_user, u2_new);

			//updating L_l' (item 2)
			//eq. L_1 = L_l' - γƞU_1
			var l1_new_item2 = u1_new.Multiply(γ * ƞ);
			l1_new_item2 = l1_item2.MinusWithReturn (l1_new_item2);
			L_1.SetRow (idx_item2, l1_new_item2);

			//updating L_l (item 1)
			//eq. L_1 = L_l + γƞU_1
			var l1_new_item1 = u1_new.Multiply (γ * ƞ);
			l1_new_item1 = l1_item1.AddWithReturn(l1_new_item1);
			L_1.SetRow (idx_item1, l1_new_item1);

			//Project the update lataent factors to enforce constraints in Eqs. (5) ~ (7),
			UpdateRelevantFactorsConstraints (U_1, ind_user, C);
			UpdateRelevantFactorsConstraints (U_2, ind_user, C * α);
			UpdateRelevantFactorsConstraints (L_1, idx_item1, C);
			UpdateRelevantFactorsConstraints (L_1, idx_item2, C);
		}

		void UpdateRelevantFactorsConstraints (Matrix<double> matrix, int rowIndex, float normalizeValue)
		{
			var row = matrix.GetRow (rowIndex);

			var norm = DataType.VectorExtensions.EuclideanNorm (row);
			if (norm > normalizeValue)
				matrix.SetRow (rowIndex, row.Divide (norm).Multiply (C)); //C*(U_1 /||U_1||)			
		}

		void InitializeLossWeight ()
		{
			lossWeight = new double [Ratings.AllItems.Count + 1];
			double lossWeightTotal = 0.0;

			for (int i = 1; i <= Ratings.AllItems.Count; i++) {
				lossWeight [i] = lossWeightTotal + 1 / i;
				lossWeightTotal = lossWeightTotal + (1 / i);
			}
		}

		/// <summary>
		/// Indicator the specified statement.
		/// </summary>
		/// <param name="statement">If set to <c>true</c> statement.</param>
		int I (bool statement)
		{
			return (statement) ? 1 : 0;
		}

		/// <summary>
		/// Ranking Incompatibility function eq(1)
		/// Measures the number of POIs that are incorrectly ranked higher than l for user u.
		/// </summary>
		/// <param name="x_score">Recommendation score of POI ℓ</param>
		/// <param name="x_freq">Users frequency of visits to POI ℓ</param>
		/// <param name="y_score">Recommendation score of POI ℓ'</param>
		/// <param name="y_freq">Users frequency of visits to POI ℓ'</param>
		int Incompatibility (double x_score, int x_freq, double y_score, int y_freq)
		{
			return I (x_freq > y_freq) * I (x_score < (y_score + ε));
		}

		/// <summary>
		/// Computs the ranking incompatibility from incompFunction into a loss eq(2)
		/// </summary>
		/// <param name="r">Rating incompatibility.</param>
		private double E (int r)
		{
			return lossWeight [r];

			//double sum = 0;
			//for (int i = 1; i <= r; i++) {
			//	sum += 1 / i;
			//}
			//return sum;
		}

		/// <summary>
		/// Used to approximate the indicator funciton. [1]
		/// </summary>
		/// <returns>The function.</returns>
		/// <param name="a">The alpha component.</param>
		private double sigmoidFunction (double a)
		{
			return (1 / (1 + Math.Exp (-a)));
		}

		/// <summary>
		/// Function for computing δuℓℓ′ [1].
		/// </summary>
		/// <returns>The function.</returns>
		/// <param name="x_score">Recommendation score of POI ℓ</param>
		/// <param name="y_score">Recommendation score of POI ℓ'</param>
		private double deltaFunction (double x_score, double y_score)
		{
			return (sigmoidFunction (y_score + ε - x_score) * (1 - (sigmoidFunction (y_score + ε - x_score))));
		}


		void SaveWeightedMatrix ()
		{
			U_1.Serialize (FILENAME_U1);
			U_2.Serialize (FILENAME_U2);
			L_1.Serialize (FILENAME_L1);
			idMapperUsers.Serialize (FILENAME_MAP_USERS);
			idMapperLocations.Serialize (FILENAME_MAP_ITEMS);
		}

		void SaveDistanceMatrix ()
		{
			distanceMatrix.Serialize (FILENAME_DISTANCE_MATRIX);
			distanceMatrixIndex.Serialize (FILENAME_DISTANCE_MATRIX_IDX);
		}


	}
}
