# Roommate Lambda

This is the backend for the Roommate project.

### Get (todo: rename)
This serves up a verification code to prove to Google that we own the domain, so that they're willing to send push notifications to it.

### Post (todo: rename)

This is where we get push notifications. The notification informs us when a calendar has changed, so we then fetch its events, look up whether there's currently any boards assigned to it, and send an updates to the boards' MQTT topics.

### UpdateRequest

This is for when a device specifically asks for an update. We look up which calendar is assigned to the requesting board, fetch events, and send an update.

### ReservationRequest

This runs when a device requests a new reservation (when you push the button on the device). We look up what calendar the board is assigned to, fetch events for it, see if the time requested is available, and conditionally create the event.

We could stop here and count on the push notification from Google to drive an update to the board. This is how it formerly worked, but we found the delay to be long and variable. Now we poll until we see that the event is created and the attendee has accepted the room, and send an update to the board from here.

(It's probably followed, a few seconds later, by a duplicate update from the push notification handler)

### OnDeviceConnect

This fires when a device connects, and sends it an update.

### RenewWebhooks

This runs nightly to renew our push notification subscriptions. Currently, we request a new 24h subscription each night (with a new ID). So, it's possible that there's a small gap without a subscription, or a small overlap where we have two.
