# label-triggered-launch

A journal for shared dispatch decisions and an observation-only watcher. Issue #313 seeded it; the
P0 control-plane slices hardened it into the fence a trusted authority will consume. No component
dispatches: a Run starts only through controlled admission with a durable effect id.
