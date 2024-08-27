#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Common
{
	public static class WorldExtensions
	{
		/// <summary>
		/// Filters <paramref name="actors"/> by only returning those that can be reached as the target of a path from
		/// <paramref name="sourceActor"/>. Only terrain is taken into account, i.e. as if
		/// <see cref="BlockedByActor.None"/> was given.
		/// <paramref name="targetOffsets"/> is used to define locations around each actor in <paramref name="actors"/>
		/// of which one must be reachable.
		/// </summary>
		public static IEnumerable<(Actor Actor, WVec[] ReachableOffsets)> WithPathFrom(
			this IEnumerable<Actor> actors, Actor sourceActor, Func<Actor, WVec[]> targetOffsets)
		{
			if (sourceActor.Info.HasTraitInfo<AircraftInfo>())
				return actors.Select<Actor, (Actor Actor, WVec[] ReachableOffsets)>(a => (a, targetOffsets(a)));
			var mobile = sourceActor.TraitOrDefault<Mobile>();
			if (mobile == null)
				return Enumerable.Empty<(Actor Actor, WVec[] ReachableOffsets)>();

			var pathFinder = sourceActor.World.WorldActor.Trait<PathFinder>();
			var locomotor = mobile.Locomotor;
			var map = sourceActor.World.Map;
			return actors
				.Select<Actor, (Actor Actor, WVec[] ReachableOffsets)>(a =>
				{
					return (a, targetOffsets(a).Where(offset =>
						pathFinder.PathExistsForLocomotor(
							mobile.Locomotor,
							map.CellContaining(sourceActor.CenterPosition),
							map.CellContaining(a.CenterPosition + offset)))
						.ToArray());
				})
				.Where(x => x.ReachableOffsets.Length > 0);
		}

		/// <summary>
		/// Filters <paramref name="actors"/> by only returning those that can be reached as the target of a path from
		/// <paramref name="sourceActor"/>. Only terrain is taken into account, i.e. as if
		/// <see cref="BlockedByActor.None"/> was given.
		/// </summary>
		public static IEnumerable<Actor> WithPathFrom(this IEnumerable<Actor> actors, Actor sourceActor)
		{
			return actors.WithPathFrom(sourceActor, _ => new[] { WVec.Zero }).Select(x => x.Actor);
		}

		/// <summary>
		/// Of <paramref name="actors"/> that can be reached as the target of a path from
		/// <paramref name="sourceActor"/>, returns the nearest by comparing their <see cref="Actor.CenterPosition"/>.
		/// Only terrain is taken into account, i.e. as if <see cref="BlockedByActor.None"/> was given.
		/// <paramref name="targetOffsets"/> is used to define locations around each actor in <paramref name="actors"/>
		/// of which one must be reachable.
		/// </summary>
		public static Actor ClosestToWithPathFrom(
			this IEnumerable<Actor> actors, Actor sourceActor, Func<Actor, WVec[]> targetOffsets = null)
		{
			return actors
				.WithPathFrom(sourceActor, targetOffsets ?? (_ => new[] { WVec.Zero }))
				.Select(x => x.Actor)
				.ClosestToIgnoringPath(sourceActor);
		}

		/// <summary>
		/// Of <paramref name="positions"/> that can be reached as the target of a path from
		/// <paramref name="sourceActor"/>, returns the nearest by comparing the <see cref="Actor.CenterPosition"/>.
		/// Only terrain is taken into account, i.e. as if <see cref="BlockedByActor.None"/> was given.
		/// </summary>
		public static WPos? ClosestToWithPathFrom(this IEnumerable<WPos> positions, Actor sourceActor)
		{
			if (sourceActor.Info.HasTraitInfo<AircraftInfo>())
				return positions.ClosestToIgnoringPath(sourceActor.CenterPosition);
			var mobile = sourceActor.TraitOrDefault<Mobile>();
			if (mobile == null)
				return null;

			var pathFinder = sourceActor.World.WorldActor.Trait<PathFinder>();
			var locomotor = mobile.Locomotor;
			var map = sourceActor.World.Map;
			return positions
				.Where(p => pathFinder.PathExistsForLocomotor(
					locomotor,
					map.CellContaining(sourceActor.CenterPosition),
					map.CellContaining(p)))
				.ClosestToIgnoringPath(sourceActor.CenterPosition);
		}

		/// <summary>
		/// Filters <paramref name="actors"/> by only returning those where the <paramref name="targetPosition"/> can
		/// be reached as the target of a path from the actor. Only terrain is taken into account, i.e. as if
		/// <see cref="BlockedByActor.None"/> was given.
		/// </summary>
		public static IEnumerable<Actor> WithPathTo(this IEnumerable<Actor> actors, World world, WPos targetPosition)
		{
			var pathFinder = world.WorldActor.Trait<PathFinder>();
			var map = world.Map;
			return actors
				.Where(a =>
				{
					if (a.Info.HasTraitInfo<AircraftInfo>())
						return true;
					var mobile = a.TraitOrDefault<Mobile>();
					if (mobile == null)
						return false;
					return pathFinder.PathExistsForLocomotor(
						mobile.Locomotor,
						map.CellContaining(targetPosition),
						map.CellContaining(a.CenterPosition));
				});
		}

		/// <summary>
		/// Filters <paramref name="actors"/> by only returning those where any of the
		/// <paramref name="targetPositions"/> can be reached as the target of a path from the actor.
		/// Returns the reachable target positions for each actor.
		/// Only terrain is taken into account, i.e. as if <see cref="BlockedByActor.None"/> was given.
		/// </summary>
		public static IEnumerable<(Actor Actor, WPos[] ReachablePositions)> WithPathToAny(
			this IEnumerable<Actor> actors, World world, Func<Actor, WPos[]> targetPositions)
		{
			var pathFinder = world.WorldActor.Trait<PathFinder>();
			var map = world.Map;
			return actors
				.Select<Actor, (Actor Actor, WPos[] ReachablePositions)>(a =>
				{
					if (a.Info.HasTraitInfo<AircraftInfo>())
						return (a, targetPositions(a).ToArray());
					var mobile = a.TraitOrDefault<Mobile>();
					if (mobile == null)
						return (a, Array.Empty<WPos>());
					return (a, targetPositions(a).Where(targetPosition =>
						pathFinder.PathExistsForLocomotor(
							mobile.Locomotor,
							map.CellContaining(targetPosition),
							map.CellContaining(a.CenterPosition)))
						.ToArray());
				})
				.Where(x => x.ReachablePositions.Length > 0);
		}

		/// <summary>
		/// Filters <paramref name="actors"/> by only returning those where the <paramref name="targetActor"/> can be
		/// reached as the target of a path from the actor. Only terrain is taken into account, i.e. as if
		/// <see cref="BlockedByActor.None"/> was given.
		/// </summary>
		public static IEnumerable<Actor> WithPathTo(this IEnumerable<Actor> actors, Actor targetActor)
		{
			return actors.WithPathTo(targetActor.World, targetActor.CenterPosition);
		}

		/// <summary>
		/// Of <paramref name="actors"/> where the <paramref name="targetPosition"/> can be reached as the target of a
		/// path from the actor, returns the nearest by comparing the <see cref="Actor.CenterPosition"/>.
		/// Only terrain is taken into account, i.e. as if <see cref="BlockedByActor.None"/> was given.
		/// </summary>
		public static Actor ClosestToWithPathTo(this IEnumerable<Actor> actors, World world, WPos targetPosition)
		{
			return actors
				.WithPathTo(world, targetPosition)
				.ClosestToIgnoringPath(targetPosition);
		}

		/// <summary>
		/// Of <paramref name="actors"/> where any of the <paramref name="targetPositions"/> can be reached as the
		/// target of a path from the actor, returns the nearest by comparing the <see cref="Actor.CenterPosition"/>.
		/// Only terrain is taken into account, i.e. as if <see cref="BlockedByActor.None"/> was given.
		/// </summary>
		public static Actor ClosestToWithPathToAny(
			this IEnumerable<Actor> actors, World world, Func<Actor, WPos[]> targetPositions)
		{
			return actors
				.WithPathToAny(world, targetPositions)
				.MinByOrDefault(x => x.ReachablePositions.Min(pos => (x.Actor.CenterPosition - pos).LengthSquared))
				.Actor;
		}

		/// <summary>
		/// Of <paramref name="actors"/> where the <paramref name="targetActor"/> can be reached as the target of a
		/// path from the actor, returns the nearest by comparing their <see cref="Actor.CenterPosition"/>.
		/// Only terrain is taken into account, i.e. as if <see cref="BlockedByActor.None"/> was given.
		/// </summary>
		public static Actor ClosestToWithPathTo(this IEnumerable<Actor> actors, Actor targetActor)
		{
			return actors.ClosestToWithPathTo(targetActor.World, targetActor.CenterPosition);
		}

		/// <summary>
		/// 找到所有与两点之间的一条线（具有可定义宽度）相交的演员（Actors）。
		/// </summary>
		/// <param name="world">进行线段相交检查的引擎世界。</param>
		/// <param name="lineStart">线段的起始位置。</param>
		/// <param name="lineEnd">线段的结束位置。</param>
		/// <param name="lineWidth">线段宽度，决定了演员的健康半径（Health Radius）与线段之间的最小距离。</param>
		/// <param name="onlyBlockers">如果设置为 true，则仅考虑具有 <see cref="IBlocksProjectiles"/> 特性的演员的大小，这可能提高搜索性能。
		/// 但不会基于此特性过滤返回的演员列表。</param>
		/// <returns>返回所有与线段相交的演员列表。</returns>
		public static IEnumerable<Actor> FindActorsOnLine(
			this World world, WPos lineStart, WPos lineEnd, WDist lineWidth, bool onlyBlockers = false)
		{
			// 通过首先在起点和终点之间找到一个矩形内的所有演员来进行线段相交检查。
			// 然后我们遍历这个列表，找到所有健康半径至少在线段宽度范围内的演员。
			// 对于没有健康半径的演员，我们只检查它们的中心点。
			// 选择的矩形必须足够大，以覆盖整个线段的宽度。
			// xDir 和 yDir 永远不能为 0，否则在相应方向上的 overscan（超选）将为 0。
			var xDiff = lineEnd.X - lineStart.X;
			var yDiff = lineEnd.Y - lineStart.Y;
			var xDir = xDiff < 0 ? -1 : 1;
			var yDir = yDiff < 0 ? -1 : 1;

			// 根据线段的方向向量 dir，计算超选区域的大小。
			var dir = new WVec(xDir, yDir, 0);
			var largestValidActorRadius = onlyBlockers ? world.ActorMap.LargestBlockingActorRadius.Length : world.ActorMap.LargestActorRadius.Length;
			var overselect = dir * (1024 + lineWidth.Length + largestValidActorRadius);

			// 确定选择区域的起点和终点，该区域必须包括线段的宽度。
			var finalTarget = lineEnd + overselect;
			var finalSource = lineStart - overselect;

			// 查找选定矩形内的所有演员。
			var actorsInSquare = world.ActorMap.ActorsInBox(finalTarget, finalSource);
			var intersectedActors = new List<Actor>();

			foreach (var currActor in actorsInSquare)
			{
				var actorWidth = 0;

				// 性能优化：避免使用 TraitsImplementing<HitShape>，因为它需要在特性字典中查找演员。
				foreach (var targetPos in currActor.EnabledTargetablePositions)
					if (targetPos is HitShape hitshape)
						actorWidth = Math.Max(actorWidth, hitshape.Info.Type.OuterRadius.Length);

				// 计算演员的中心点到线段的最小距离。
				var projection = lineStart.MinimumPointLineProjection(lineEnd, currActor.CenterPosition);
				var distance = (currActor.CenterPosition - projection).HorizontalLength;

				// 如果演员的中心点到线段的最小距离小于或等于线段宽度和演员宽度之和，则认为该演员与线段相交。
				var maxReach = actorWidth + lineWidth.Length;

				if (distance <= maxReach)
					intersectedActors.Add(currActor);
			}

			// 返回所有与线段相交的演员列表。
			return intersectedActors;
		}

		public static IEnumerable<Actor> FindBlockingActorsOnLine(this World world, WPos lineStart, WPos lineEnd, WDist lineWidth)
		{
			return world.FindActorsOnLine(lineStart, lineEnd, lineWidth, true);
		}

		/// <summary>
		/// Finds all the actors of which their health radius might be intersected by a specified circle.
		/// </summary>
		public static IEnumerable<Actor> FindActorsOnCircle(this World world, WPos origin, WDist r)
		{
			return world.FindActorsInCircle(origin, r + world.ActorMap.LargestActorRadius);
		}

		/// <summary>
		/// 在直线 (A-B) 上找到最接近目标点 (C) 的点 (D)。
		/// </summary>
		/// <param name="lineStart">直线的起点（线段的尾部）。</param>
		/// <param name="lineEnd">直线的终点（线段的头部）。</param>
		/// <param name="point">要找到最小距离的目标点 (C)。</param>
		/// <returns>返回在直线上最接近目标点的点 (D)。</returns>
		public static WPos MinimumPointLineProjection(this WPos lineStart, WPos lineEnd, WPos point)
		{
			// 计算线段的平方长度，避免使用浮点数进行距离计算
			var squaredLength = (lineEnd - lineStart).HorizontalLengthSquared;

			// 如果线段长度为零（起点和终点重合），则返回终点作为最接近点
			if (squaredLength == 0)
				return lineEnd;

			// 将线段延长为无限长的直线，并计算点到直线的投影。
			// 投影点位于参数化方程 target + t * (source - target) 的位置
			// 其中 t = [(point - target) . (source - target)] / |source - target|^2
			// 通常的点积计算为 (xDiff + yDiff) / dist，其中 dist = (target - source).LengthSquared
			// 为了避免使用浮点数，暂时不进行除法运算，而是尽可能处理大数值，
			// 然后在点积乘法之后再除以 dist。
			var xDiff = ((long)point.X - lineEnd.X) * (lineStart.X - lineEnd.X);
			var yDiff = ((long)point.Y - lineEnd.Y) * (lineStart.Y - lineEnd.Y);
			var t = xDiff + yDiff;

			// 如果投影点在终点 (B) 之外，则返回终点 (B) 作为最接近点
			if (t < 0)
				return lineEnd;

			// 如果投影点在起点 (A) 之外，则返回起点 (A) 作为最接近点
			if (t > squaredLength)
				return lineStart;

			// 如果投影点在线段 (A-B) 上，则返回该投影点
			return WPos.Lerp(lineEnd, lineStart, t, squaredLength);
		}
	}
}
