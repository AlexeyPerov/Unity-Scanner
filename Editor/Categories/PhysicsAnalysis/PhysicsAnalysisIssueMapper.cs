using System.Collections.Generic;
using System.Linq;
using UnityScanner.Core.Issues;
using UnityScanner.Core.Settings;
using UnityEngine;

namespace UnityScanner.Categories.PhysicsAnalysis
{
    public static class PhysicsAnalysisIssueMapper
    {
        public static List<UnityScannerIssue> MapIssues(
            List<ScenePhysicsData> results,
            PhysicsAnalysisSettings settings,
            PlatformProfile profile)
        {
            var issues = new List<UnityScannerIssue>();
            if (profile == null) return issues;

            foreach (var data in results)
            {
                if (settings.CheckRigidbodyExceeded && data.RigidbodyCount > profile.MaxRigidbodyCount)
                {
                    issues.Add(MakeIssue("physics_rigidbody_exceeded",
                        "Scene has " + data.RigidbodyCount + " rigidbodies, threshold is " + profile.MaxRigidbodyCount + ".",
                        UnityScannerIssueSeverity.Warning, data.ScenePath,
                        "RigidbodyCount", data.RigidbodyCount,
                        "ThresholdCount", profile.MaxRigidbodyCount));
                }

                if (settings.CheckStaticColliderOnMovingParent)
                {
                    foreach (var col in data.Colliders)
                    {
                        if (col.IsStatic && col.HasMovingParent)
                        {
                            issues.Add(MakeIssue("physics_static_collider_on_moving_parent",
                                "Static collider '" + col.ObjectPath + "' has moving parent.",
                                UnityScannerIssueSeverity.Warning, data.ScenePath,
                                "ColliderPath", col.ObjectPath));
                        }
                    }
                }

                if (settings.CheckNoGravityNoConstraints)
                {
                    foreach (var rb in data.Rigidbodies)
                    {
                        if (!rb.IsKinematic && !rb.UseGravity && rb.Constraints == 0)
                        {
                            issues.Add(MakeIssue("physics_rigidbody_no_gravity_no_constraints",
                                "Non-kinematic Rigidbody '" + rb.ObjectPath + "' has no gravity and no constraints.",
                                UnityScannerIssueSeverity.Info, data.ScenePath,
                                "RigidbodyPath", rb.ObjectPath));
                        }
                    }
                }

                if (settings.CheckTriggerNonKinematic)
                {
                    foreach (var rb in data.Rigidbodies)
                    {
                        if (rb.IsTrigger && !rb.IsKinematic)
                        {
                            issues.Add(MakeIssue("physics_trigger_non_kinematic",
                                "Trigger Rigidbody '" + rb.ObjectPath + "' is not kinematic.",
                                UnityScannerIssueSeverity.Info, data.ScenePath,
                                "RigidbodyPath", rb.ObjectPath));
                        }
                    }
                }

                if (settings.CheckInterpolationUnnecessary)
                {
                    foreach (var rb in data.Rigidbodies)
                    {
                        if (!rb.IsKinematic && rb.InterpolationMode != 0)
                        {
                            issues.Add(MakeIssue("physics_interpolation_unnecessary",
                                "Rigidbody '" + rb.ObjectPath + "' uses interpolation mode " + (RigidbodyInterpolation)rb.InterpolationMode + ".",
                                UnityScannerIssueSeverity.Verbose, data.ScenePath,
                                "RigidbodyPath", rb.ObjectPath,
                                "InterpolationMode", rb.InterpolationMode));
                        }
                    }
                }

                if (settings.CheckMeshColliderComplex)
                {
                    foreach (var col in data.Colliders)
                    {
                        if (col.ColliderType == "MeshCollider" && col.TriangleCount > profile.MaxMeshColliderTriangles)
                        {
                            issues.Add(MakeIssue("physics_mesh_collider_complex",
                                "Mesh collider '" + col.ObjectPath + "' has " + col.TriangleCount + " triangles (threshold: " + profile.MaxMeshColliderTriangles + ").",
                                UnityScannerIssueSeverity.Warning, data.ScenePath,
                                "ColliderPath", col.ObjectPath,
                                "TriangleCount", col.TriangleCount,
                                "ThresholdTriangles", profile.MaxMeshColliderTriangles));
                        }
                    }
                }

                if (settings.CheckConcaveMeshKinematic)
                {
                    foreach (var col in data.Colliders)
                    {
                        if (col.ColliderType == "MeshCollider" && !col.IsConvex)
                        {
                            var hasNonKinematicRb = data.Rigidbodies.Any(r => r.ObjectPath == col.ObjectPath && !r.IsKinematic);
                            if (hasNonKinematicRb)
                            {
                                issues.Add(MakeIssue("physics_concave_mesh_kinematic",
                                    "Concave mesh collider on non-kinematic body '" + col.ObjectPath + "'. Unity will auto-convert to convex.",
                                    UnityScannerIssueSeverity.Info, data.ScenePath,
                                    "ColliderPath", col.ObjectPath));
                            }
                        }
                    }
                }

                if (settings.CheckMissingMaterial && data.RigidbodyCount > 0)
                {
                    foreach (var col in data.Colliders)
                    {
                        if (!col.HasPhysicsMaterial && !col.IsTrigger)
                        {
                            issues.Add(MakeIssue("physics_missing_material",
                                "Collider '" + col.ObjectPath + "' has no physics material.",
                                UnityScannerIssueSeverity.Info, data.ScenePath,
                                "ColliderPath", col.ObjectPath));
                        }
                    }
                }

                if (settings.CheckLayerMatrixBloat)
                {
                    foreach (var pair in data.LayerCollisions)
                    {
                        if (pair.Enabled && (pair.ColliderCountA == 0 || pair.ColliderCountB == 0))
                        {
                            issues.Add(MakeIssue("physics_layer_matrix_bloat",
                                "Layer " + LayerMask.LayerToName(pair.LayerA) + " (" + pair.LayerA + ") <-> " +
                                LayerMask.LayerToName(pair.LayerB) + " (" + pair.LayerB + ") enabled but one side has 0 colliders.",
                                UnityScannerIssueSeverity.Info, data.ScenePath,
                                "LayerA", pair.LayerA,
                                "LayerB", pair.LayerB,
                                "ColliderCountA", pair.ColliderCountA,
                                "ColliderCountB", pair.ColliderCountB));
                        }
                    }
                }
            }

            return issues;
        }

        private static UnityScannerIssue MakeIssue(
            string code, string description, UnityScannerIssueSeverity severity,
            string assetPath, params object[] metadataPairs)
        {
            var issue = new UnityScannerIssue
            {
                CategoryId = "physics_analysis",
                IssueCode = code,
                Description = description,
                Severity = severity,
                AssetPath = assetPath,
                FixId = "none"
            };

            for (var i = 0; i + 1 < metadataPairs.Length; i += 2)
            {
                if (metadataPairs[i] is string key)
                    issue.Metadata[key] = metadataPairs[i + 1];
            }

            return issue;
        }
    }
}
