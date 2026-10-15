## VERSION 0.3.2

- Automatically import legacy CommandQueue / CommandQueueExtended presets from BepInEx/config/commandQueueSaves.json once per Remastered preset file. Keep the source unchanged and retain existing Remastered presets.
- Persist import completion with the presets, including after imported presets are deleted. Retain names, item order and quantities; split stacks above 999. Legacy presets contain no Repeat settings.
- Continue reading existing Remastered preset arrays. Invalid legacy input aborts the complete import and is logged for retry on the next startup.
