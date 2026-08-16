//! Reconciling the desired set of shortcuts with what's already in
//! `shortcuts.vdf`.
//!
//! The rule is conservative: foreign entries (anything without [`MANAGED_TAG`],
//! e.g. a hand-made "SISR Marker") are never touched. Among our own managed
//! entries, ones no longer wanted are removed, ones still wanted are kept as-is
//! (preserving `LastPlayTime` and any other state Steam wrote), and genuinely
//! new games are added.

use crate::shortcut::MANAGED_TAG;
use crate::vdf::Shortcut;
use std::collections::HashSet;

/// The outcome of reconciling desired against existing shortcuts.
#[derive(Debug, Default)]
pub struct SyncPlan {
    /// New shortcuts to write.
    pub added: Vec<Shortcut>,
    /// Managed shortcuts to drop (no longer installed/desired).
    pub removed: Vec<Shortcut>,
    /// Managed shortcuts already present and still desired (kept unchanged).
    pub kept: Vec<Shortcut>,
    /// Non-managed shortcuts, preserved verbatim.
    pub foreign: Vec<Shortcut>,
}

impl SyncPlan {
    /// Whether applying this plan would change anything on disk.
    pub fn is_noop(&self) -> bool {
        self.added.is_empty() && self.removed.is_empty()
    }
}

fn is_managed(sc: &Shortcut) -> bool {
    sc.tags.iter().any(|t| t == MANAGED_TAG)
}

/// Compute what to add, remove and keep. Matching is by `app_id`, which is
/// deterministic from the exe+name, so re-running is idempotent.
pub fn plan(existing: &[Shortcut], desired: &[Shortcut]) -> SyncPlan {
    let desired_ids: HashSet<i32> = desired.iter().map(|s| s.app_id).collect();
    let existing_managed_ids: HashSet<i32> = existing
        .iter()
        .filter(|s| is_managed(s))
        .map(|s| s.app_id)
        .collect();

    let mut out = SyncPlan::default();
    for sc in existing {
        if !is_managed(sc) {
            out.foreign.push(sc.clone());
        } else if desired_ids.contains(&sc.app_id) {
            out.kept.push(sc.clone());
        } else {
            out.removed.push(sc.clone());
        }
    }
    for sc in desired {
        if !existing_managed_ids.contains(&sc.app_id) {
            out.added.push(sc.clone());
        }
    }
    out
}

/// Materialize the final shortcut list to serialize: foreign entries first
/// (unchanged), then kept managed entries, then newly added ones.
pub fn apply(plan: &SyncPlan) -> Vec<Shortcut> {
    let mut out = Vec::with_capacity(plan.foreign.len() + plan.kept.len() + plan.added.len());
    out.extend(plan.foreign.iter().cloned());
    out.extend(plan.kept.iter().cloned());
    out.extend(plan.added.iter().cloned());
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn managed(app_id: i32, name: &str) -> Shortcut {
        Shortcut {
            app_id,
            app_name: name.into(),
            tags: vec![MANAGED_TAG.into()],
            ..Default::default()
        }
    }

    fn foreign(app_id: i32, name: &str) -> Shortcut {
        Shortcut {
            app_id,
            app_name: name.into(),
            tags: vec!["something-else".into()],
            ..Default::default()
        }
    }

    #[test]
    fn foreign_entries_are_never_touched() {
        let existing = vec![foreign(1, "SISR Marker")];
        let p = plan(&existing, &[]);
        assert_eq!(p.foreign.len(), 1);
        assert!(p.removed.is_empty());
        assert!(p.is_noop());
        assert_eq!(apply(&p).len(), 1);
    }

    #[test]
    fn adds_removes_and_keeps() {
        let existing = vec![
            foreign(1, "SISR Marker"),
            managed(2, "Still Installed"),
            managed(3, "Gone"),
        ];
        let desired = vec![managed(2, "Still Installed"), managed(4, "Brand New")];

        let p = plan(&existing, &desired);
        assert_eq!(p.added.iter().map(|s| s.app_id).collect::<Vec<_>>(), [4]);
        assert_eq!(p.removed.iter().map(|s| s.app_id).collect::<Vec<_>>(), [3]);
        assert_eq!(p.kept.iter().map(|s| s.app_id).collect::<Vec<_>>(), [2]);
        assert_eq!(p.foreign.iter().map(|s| s.app_id).collect::<Vec<_>>(), [1]);

        let final_list = apply(&p);
        assert_eq!(
            final_list.iter().map(|s| s.app_id).collect::<Vec<_>>(),
            [1, 2, 4]
        );
    }

    #[test]
    fn is_idempotent() {
        let desired = vec![managed(2, "A")];
        let first = apply(&plan(&[], &desired));
        let second = plan(&first, &desired);
        assert!(second.is_noop());
    }
}
