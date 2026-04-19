'use strict';

require('dotenv').config();

const express = require('express');
const { addToCollection, removeFromCollection } = require('./bggWriter');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 3001;

app.post('/collection/add', async (req, res) => {
	const { username, bggId } = req.body;

	if (!username || !bggId) {
		return res.status(400).json({ error: 'username and bggId are required' });
	}

	try {
		const collId = await addToCollection(username, bggId);
		return res.status(200).json({ collId });
	} catch (err) {
		console.error('[bgg-writer] addToCollection failed:', err.message);
		return res.status(500).json({ error: err.message });
	}
});

app.post('/collection/remove', async (req, res) => {
	const { username, collId } = req.body;

	if (!username || !collId) {
		return res.status(400).json({ error: 'username and collId are required' });
	}

	try {
		await removeFromCollection(username, collId);
		return res.status(200).json({});
	} catch (err) {
		console.error('[bgg-writer] removeFromCollection failed:', err.message);
		return res.status(500).json({ error: err.message });
	}
});

app.listen(PORT, () => {
	console.log(`[bgg-writer] listening on port ${PORT}`);
});
