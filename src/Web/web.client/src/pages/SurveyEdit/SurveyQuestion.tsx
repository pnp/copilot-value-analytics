import React, { ChangeEvent } from "react";
import { SurveyQuestionDB } from "../../apimodels/Models";
import { Checkbox, Field, Input, Link, Select, SelectOnChangeData } from "@fluentui/react-components";
import { LogicalOperator, QuestionDatatype } from "../../apimodels/Enums";

export const SurveyQuestion: React.FC<{ q: SurveyQuestionDB, deleteQuestion: Function }> = (props) => {

  const [question, setQuestion] = React.useState<string>(props.q.question);
  const [logicalOp, setLogicalOp] = React.useState<LogicalOperator>(props.q.optimalAnswerLogicalOp ?? LogicalOperator.Equals);
  const [dataType, setDataType] = React.useState<QuestionDatatype>(props.q.dataType ?? QuestionDatatype.String);
  const [hasOptimalAnswerValue, setHasOptimalAnswerValue] = React.useState<boolean>(props.q.optimalAnswerValue !== null);
  const [optimalAnswerValue, setOptimalAnswerValue] = React.useState<string | undefined>(props.q.optimalAnswerValue);

  const hasOptimalAnswerValueClick = React.useCallback(() => {
    setHasOptimalAnswerValue(!hasOptimalAnswerValue);
  }, [hasOptimalAnswerValue]);

  const onSetDataTypeChange = React.useCallback((_ev: ChangeEvent<HTMLSelectElement>, data: SelectOnChangeData) => {
    switch (data.value) {
      case (Number(QuestionDatatype.Bool)).toString():
        setDataType(QuestionDatatype.Bool);
        break;
      case (Number(QuestionDatatype.Int)).toString():
        setDataType(QuestionDatatype.Int);
        break;
      case (Number(QuestionDatatype.String)).toString():
        setDataType(QuestionDatatype.String);
        break;
      default:
        setDataType(QuestionDatatype.Unknown);
    }
  }, []);

  const onSetLogicalOpChange = React.useCallback((_ev: ChangeEvent<HTMLSelectElement>, data: SelectOnChangeData) => {
    switch (data.value) {
      case (Number(LogicalOperator.Equals)).toString():
        setLogicalOp(LogicalOperator.Equals);
        break;
      case (Number(LogicalOperator.NotEquals)).toString():
        setLogicalOp(LogicalOperator.NotEquals);
        break;
      case (Number(LogicalOperator.GreaterThan)).toString():
        setLogicalOp(LogicalOperator.GreaterThan);
        break;
      case (Number(LogicalOperator.LessThan)).toString():
        setLogicalOp(LogicalOperator.LessThan);
        break;
      default:
        setLogicalOp(LogicalOperator.Unknown);
    }
  }, []);

  return (
    <div className="pageQuestion">
      <h3>---</h3>
      <Field label="Question text">
        <Input onChange={(e) => setQuestion(e.target.value)} value={question} />
      </Field>
      <Field label="Optimal value">
        <Input onChange={(e) => setOptimalAnswerValue(e.target.value)} value={optimalAnswerValue ?? ""} disabled={!hasOptimalAnswerValue} />
        <Checkbox
          checked={hasOptimalAnswerValue}
          onChange={hasOptimalAnswerValueClick}
          label="Has optimal value"
        />
      </Field>

      <div>
        <label>Data type</label>
        <Select onChange={onSetDataTypeChange} value={dataType}>
          <option value={LogicalOperator.Unknown}>Select...</option>
          <option value={QuestionDatatype.Bool}>Yes/no</option>
          <option value={QuestionDatatype.Int}>Numeric</option>
          <option value={QuestionDatatype.String}>Text</option>
        </Select>
      </div>
      <div>
        <label>Logical Operator</label>
        <Select onChange={onSetLogicalOpChange} value={logicalOp}>
          <option value={LogicalOperator.Unknown}>Select...</option>
          <option value={LogicalOperator.Equals}>Equals</option>
          <option value={LogicalOperator.NotEquals}>Not Equals</option>
          <option value={LogicalOperator.GreaterThan}>Greater Than</option>
          <option value={LogicalOperator.LessThan}>Less Than</option>
        </Select>
      </div>
      <div>
        <Link onClick={()=> props.deleteQuestion()}>Remove question</Link>
      </div>
    </div>
  );
};
