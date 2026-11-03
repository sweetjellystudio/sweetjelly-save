#   Save sample

Import Save Sample, open Sample Scene and enter Play Mode. The profile orders Serialization before Save (100 then 200). The consumer registers its own keyed values, changes and marks them dirty, saves/loads, and demonstrates force save and asynchronous deletion. Disabling and enabling it releases and restores its registrations.

The bootstrap uses save-sample.json. Save Presets requires an explicitly selected bootstrap and, in Play Mode, its running driver. The sample start scene is set to the default import path; update that field if importing to another folder. Apply leaves the Editor stopped; Apply and Play opens that explicit scene. Legacy migration and PlayerPrefs reset are separate actions.
