from flask import Flask, request, jsonify, render_template_string
from flask_cors import CORS
import logging
import sys
import os
from typing import Dict, Any

# ==================== SETUP ====================
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

sys.path.append(os.path.join(os.path.dirname(__file__), 'AI_model'))
from AI_model.Openai_api import OpenAIService

app = Flask(__name__)
CORS(app)

openai_service = OpenAIService()

# ==================== VALIDATION ====================
def validate_hint_request(data: Dict[str, Any]) -> tuple[bool, str]:
    required_fields = ['question_text', 'question_type', 'difficulty_level', 'student_answer']
    for field in required_fields:
        if field not in data:
            return False, f"Missing required field: {field}"
    return True, ""


def validate_feedback_request(data: Dict[str, Any]) -> tuple[bool, str]:
    required_fields = ['question_text', 'question_type', 'student_answer', 'correct_answer', 'is_correct']
    for field in required_fields:
        if field not in data:
            return False, f"Missing required field: {field}"
    if not isinstance(data.get('is_correct'), bool):
        return False, "is_correct must be boolean"
    return True, ""


# ==================== HINT ====================
@app.route('/api/hint', methods=['POST'])
def generate_hint():
    try:
        data = request.get_json()
        if not data:
            return jsonify({"error": "Request body must be JSON"}), 400

        is_valid, error = validate_hint_request(data)
        if not is_valid:
            return jsonify({"error": error}), 400

        question_text = data.get('question_text')
        question_type = data.get('question_type')
        difficulty_level = data.get('difficulty_level')
        student_answer = data.get('student_answer')
        hint_level = data.get('hint_level', 1)
        question_id = data.get('question_id')
        question_image_url = data.get('question_image_url')
        options = data.get('options')

        result = openai_service.generate_hint(
            question_text=question_text,
            question_type=question_type,
            difficulty_level=difficulty_level,
            student_answer=student_answer,
            hint_level=hint_level,
            options=options,
            question_id=question_id,
            question_image_url=question_image_url
        )

        return jsonify(result), 200

    except Exception as e:
        logger.error(e)
        return jsonify({"error": str(e)}), 500


# ==================== HINT BATCH ====================
@app.route('/api/hint/batch', methods=['POST'])
def generate_hints_batch():
    data = request.get_json()
    if not data or 'hints' not in data:
        return jsonify({"error": "Missing hints array"}), 400

    results = []
    for idx, hint in enumerate(data['hints']):
        try:
            result = openai_service.generate_hint(
                question_text=hint.get('question_text'),
                question_type=hint.get('question_type'),
                difficulty_level=hint.get('difficulty_level'),
                student_answer=hint.get('student_answer'),
                hint_level=hint.get('hint_level', 1),
                options=hint.get('options'),
                question_id=hint.get('question_id'),
                question_image_url=hint.get('question_image_url')
            )
            result['index'] = idx
            results.append(result)
        except Exception as e:
            results.append({"index": idx, "error": str(e)})

    return jsonify({"results": results}), 200


# ==================== FEEDBACK ====================
@app.route('/api/feedback', methods=['POST'])
def generate_feedback():
    try:
        data = request.get_json()
        if not data:
            return jsonify({"error": "Request body must be JSON"}), 400

        is_valid, error = validate_feedback_request(data)
        if not is_valid:
            return jsonify({"error": error}), 400

        question_text = data.get('question_text')
        question_type = data.get('question_type')
        student_answer = data.get('student_answer')
        correct_answer = data.get('correct_answer')
        is_correct = data.get('is_correct')
        explanation = data.get('explanation')
        attempt_id = data.get('attempt_id')
        options = data.get('options')

        result = openai_service.generate_feedback(
            question_text=question_text,
            question_type=question_type,
            student_answer=student_answer,
            correct_answer=correct_answer,
            is_correct=is_correct,
            explanation=explanation,
            options=options,
            attempt_id=attempt_id
        )

        return jsonify(result), 200

    except Exception as e:
        logger.error(e)
        return jsonify({"error": str(e)}), 500


# ==================== HEALTH ====================
@app.route('/api/health')
def health():
    return jsonify({"status": "healthy"}), 200


@app.route('/api/status')
def status():
    return jsonify({"service": "OpenAI AI API", "status": "running"}), 200

# ==================== DOCUMENTATION ====================
@app.route('/docs', methods=['GET'])
def swagger_ui():
    """Swagger-like API Documentation"""
    swagger_html = """
    <!DOCTYPE html>
    <html>
    <head>
        <title>Gemini AI API Docs</title>
        <meta charset="utf-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <style>
            body { font-family: Arial, sans-serif; margin: 0; padding: 0; background: #f4f4f4; }
            .header { background: #1976d2; color: white; padding: 20px; text-align: center; }
            .container { max-width: 900px; margin: 20px auto; background: white; padding: 20px; border-radius: 6px; }
            .endpoint { margin-bottom: 20px; padding: 15px; border-left: 4px solid #1976d2; background: #f9f9f9; }
            .method { font-weight: bold; color: #1976d2; margin-right: 10px; }
            .path { font-family: monospace; background: #e3f2fd; padding: 2px 6px; border-radius: 3px; }
            pre { background: #eee; padding: 10px; border-radius: 3px; overflow-x: auto; }
            a { color: #1976d2; text-decoration: none; }
        </style>
    </head>
    <body>
        <div class="header">
            <h1>🤖 OpenAI AI API Documentation</h1>
            <p>Version 1.0 - Flask + OpenAI AI</p>
        </div>
        <div class="container">
            <div class="endpoint">
                <div><span class="method">GET</span><span class="path">/api/health</span></div>
                <p>Check if server is running</p>
                <pre>Response: {"status": "healthy"}</pre>
            </div>

            <div class="endpoint">
                <div><span class="method">GET</span><span class="path">/api/status</span></div>
                <p>Get API status and available endpoints</p>
                <pre>Response: {"service": "OpenAI AI API", "status": "running"}</pre>
            </div>

            <div class="endpoint">
                <div><span class="method">POST</span><span class="path">/api/hint</span></div>
                <p>Generate AI hint for a question</p>
                <pre>POST /api/hint
Content-Type: application/json

{
  "question_text": "What is 2 + 2?",
  "question_type": "MultipleChoice",
  "difficulty_level": "Easy",
  "student_answer": "5",
  "hint_level": 1
}</pre>
            </div>

            <div class="endpoint">
                <div><span class="method">POST</span><span class="path">/api/hint/batch</span></div>
                <p>Generate multiple hints at once</p>
                <pre>POST /api/hint/batch
Content-Type: application/json
{
  "hints": [
    {
      "question_text": "2 + 2?",
      "question_type": "MultipleChoice",
      "difficulty_level": "Easy",
      "student_answer": "5"
    }
  ]
}</pre>
            </div>

            <div class="endpoint">
                <div><span class="method">POST</span><span class="path">/api/feedback</span></div>
                <p>Generate feedback for a student answer</p>
                <pre>POST /api/feedback
Content-Type: application/json
{
  "question_text": "2 + 2?",
  "question_type": "MultipleChoice",
  "student_answer": "5",
  "correct_answer": "4",
  "is_correct": false
}</pre>
            </div>

            <div class="endpoint">
                <div><span class="method">POST</span><span class="path">/api/feedback/batch</span></div>
                <p>Generate multiple feedbacks at once</p>
                <pre>POST /api/feedback/batch
Content-Type: application/json
{
  "feedbacks": [
    {
      "question_text": "2 + 2?",
      "question_type": "MultipleChoice",
      "student_answer": "5",
      "correct_answer": "4",
      "is_correct": false
    }
  ]
}</pre>
            </div>

            <div class="links">
                <a href="/api/health">→ Health Check</a> |
                <a href="/api/status">→ Status</a>
            </div>
        </div>
    </body>
    </html>
    """
    return render_template_string(swagger_html)



# ==================== MAIN ====================
if __name__ == '__main__':
    port = int(os.getenv('FLASK_PORT', 5000))
    app.run(host='0.0.0.0', port=port, debug=True)
